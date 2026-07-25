using Esri.ArcGISRuntime.Geometry;
using System.Text.Json;

namespace KoiPonders;

public static class AlertService
{
    public const double DefaultRadiusMiles = 5.0;

    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "farmguard-neighbors.json");

    // Known host ranges. If a pest isn't listed we fall back to
    // matching on the affected crop itself.
    private static readonly Dictionary<string, string[]> HostRanges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fall Armyworm"] = ["Corn", "Sorghum", "Rice", "Cotton"],
            ["Corn Earworm"] = ["Corn", "Sorghum", "Cotton", "Soybean"],
            ["European Corn Borer"] = ["Corn", "Sorghum"],
            ["Common Rust"] = ["Corn"],
            ["Southern Rust"] = ["Corn"],
            ["Northern Corn Leaf Blight"] = ["Corn"],
            ["Gray Leaf Spot"] = ["Corn"],
            ["Goss's Wilt"] = ["Corn"],
            ["Soybean Aphid"] = ["Soybean"],
            ["Sudden Death Syndrome"] = ["Soybean"],
            ["Soybean Cyst Nematode"] = ["Soybean"],
            ["Sugarcane Aphid"] = ["Sorghum"],
            ["Wheat Rust"] = ["Wheat"],
            ["Stripe Rust"] = ["Wheat"],
        };

    private static readonly string[] CropKeywords =
        ["Corn", "Soybean", "Sorghum", "Wheat", "Cotton", "Rice", "Alfalfa", "Milo"];

    // ---------- matching ----------

    public static List<AlertRecipient> ComputeRecipients(
        Incident incident,
        IEnumerable<NeighborFarm> farms,
        double radiusMiles = DefaultRadiusMiles)
    {
        var results = new List<AlertRecipient>();
        if (incident.Location is null) return results;

        foreach (var farm in farms)
        {
            if (farm.Location is null) continue;

            double miles;
            try
            {
                var a = Project(incident.Location, SpatialReferences.Wgs84);
                var b = Project(farm.Location, SpatialReferences.Wgs84);
                if (a is null || b is null) continue;

                miles = GeometryEngine.DistanceGeodetic(
                    a, b, LinearUnits.Miles, AngularUnits.Degrees,
                    GeodeticCurveType.Geodesic).Distance;
            }
            catch { continue; }

            if (miles > radiusMiles) continue;

            string? reason = SusceptibilityReason(incident, farm.Crop);
            if (reason is null) continue;

            results.Add(new AlertRecipient
            {
                Farm = farm,
                DistanceMiles = miles,
                Reason = reason
            });
        }

        results.Sort((x, y) => x.DistanceMiles.CompareTo(y.DistanceMiles));
        return results;
    }

    /// <summary>Returns why this farm is at risk, or null if it isn't.</summary>
    private static string? SusceptibilityReason(Incident incident, string neighborCrop)
    {
        if (string.IsNullOrWhiteSpace(neighborCrop)) return null;

        if (HostRanges.TryGetValue(incident.PestName, out var hosts))
        {
            var hit = hosts.FirstOrDefault(h =>
                neighborCrop.Contains(h, StringComparison.OrdinalIgnoreCase));

            if (hit is not null)
                return $"{hit} is a known host";
        }

        // Fallback — same crop as the affected field.
        foreach (var kw in CropKeywords)
        {
            bool inAffected = incident.AffectedCrop.Contains(kw, StringComparison.OrdinalIgnoreCase);
            bool inNeighbor = neighborCrop.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (inAffected && inNeighbor)
                return $"same crop ({kw})";
        }

        return null;
    }

    // ---------- neighbor registry ----------

    private static List<NeighborFarm>? _cache;

    public static async Task<List<NeighborFarm>> GetFarmsAsync()
    {
        if (_cache is not null) return _cache;

        try
        {
            if (File.Exists(FilePath))
            {
                var dtos = JsonSerializer.Deserialize<List<FarmDto>>(
                    await File.ReadAllTextAsync(FilePath));

                if (dtos is not null)
                {
                    _cache = dtos.Select(d => new NeighborFarm
                    {
                        FarmName = d.FarmName,
                        Owner = d.Owner,
                        Crop = d.Crop,
                        Acres = d.Acres,
                        Location = new MapPoint(d.Lon, d.Lat, SpatialReferences.Wgs84)
                    }).ToList();

                    return _cache;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlertService] load failed: {ex.Message}");
        }

        _cache = new List<NeighborFarm>();
        return _cache;
    }

    /// <summary>
    /// Drops a ring of neighbouring farms around a point, with varied crops so the
    /// susceptibility filter has something to actually filter on.
    /// </summary>
    public static async Task<List<NeighborFarm>> SeedAroundAsync(MapPoint center)
    {
        var wgs = Project(center, SpatialReferences.Wgs84);
        if (wgs is null) return await GetFarmsAsync();

        // Roughly: 1 deg lat ~= 69 mi, 1 deg lon ~= 69*cos(lat) mi.
        double latPerMile = 1.0 / 69.0;
        double lonPerMile = 1.0 / (69.0 * Math.Cos(wgs.Y * Math.PI / 180.0));

        (string name, string owner, string crop, double acres, double n, double e)[] spec =
        [
            ("Halvorsen Pivot North", "D. Halvorsen", "Yellow Field Corn (Dent)",  96.4,  2.1,  0.8),
            ("Rustad Section 14",     "M. Rustad",    "Soybean",                  120.2, -1.6,  2.4),
            ("Kepler Quarter East",   "J. Kepler",    "Yellow Field Corn (Dent)",  78.9,  0.4, -3.1),
            ("Vandermeer Flats",      "A. Vandermeer","Grain Sorghum (Milo)",      64.5, -3.4, -1.2),
            ("Okonkwo Dryland",       "C. Okonkwo",   "Winter Wheat",             142.7,  4.2,  1.9),
            ("Beltran Circle 7",      "R. Beltran",   "Yellow Field Corn (Dent)", 105.3, -0.9, -4.6),
        ];

        var farms = spec.Select(s => new NeighborFarm
        {
            FarmName = s.name,
            Owner = s.owner,
            Crop = s.crop,
            Acres = s.acres,
            Location = new MapPoint(
                wgs.X + (s.e * lonPerMile),
                wgs.Y + (s.n * latPerMile),
                SpatialReferences.Wgs84)
        }).ToList();

        _cache = farms;
        await SaveAsync(farms);
        return farms;
    }

    private static async Task SaveAsync(List<NeighborFarm> farms)
    {
        try
        {
            var dtos = farms.Where(f => f.Location is not null).Select(f => new FarmDto
            {
                FarmName = f.FarmName,
                Owner = f.Owner,
                Crop = f.Crop,
                Acres = f.Acres,
                Lat = f.Location!.Y,
                Lon = f.Location!.X
            }).ToList();

            await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(dtos));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlertService] save failed: {ex.Message}");
        }
    }

    private static MapPoint? Project(MapPoint p, SpatialReference target)
    {
        try
        {
            if (p.SpatialReference is null) return null;
            if (p.SpatialReference.Wkid == target.Wkid) return p;
            return GeometryEngine.Project(p, target) as MapPoint;
        }
        catch { return null; }
    }

    private class FarmDto
    {
        public string FarmName { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Crop { get; set; } = "";
        public double Acres { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}