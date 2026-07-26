using Esri.ArcGISRuntime.Geometry;
using System.Text.Json;

namespace KoiPonders;

public static class FarmStore
{
    private const string DemoPresetFileName = "farmassist_demo_preset.json";

    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "farmguard.json");

    private class ParcelDto
    {
        public string Name { get; set; } = "";
        public string Crop { get; set; } = "";
        public double Acres { get; set; }
        public DateTime MappedDate { get; set; }
        public string GeometryJson { get; set; } = "";
    }

    private class IncidentDto
    {
        public string Classification { get; set; } = "";
        public string PestName { get; set; } = "";
        public string Severity { get; set; } = "";
        public string AffectedCrop { get; set; } = "";
        public string Status { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Treatment { get; set; } = "";
        public string FieldName { get; set; } = "";
        public int Confidence { get; set; }
        public DateTime ReportDate { get; set; }
        public string LocationJson { get; set; } = "";
    }

    private class Snapshot
    {
        public List<ParcelDto> Parcels { get; set; } = new();
        public List<IncidentDto> Incidents { get; set; } = new();
    }

    public static async Task SaveAsync(IEnumerable<Parcel> parcels,
                                       IEnumerable<Incident> incidents)
    {
        try
        {
            var snap = new Snapshot
            {
                Parcels = parcels.Where(p => p.Geometry is not null)
                    .Select(p => new ParcelDto
                    {
                        Name = p.Name,
                        Crop = p.Crop,
                        Acres = p.Acres,
                        MappedDate = p.MappedDate,
                        GeometryJson = p.Geometry!.ToJson()
                    }).ToList(),

                Incidents = incidents.Where(i => i.Location is not null)
                    .Select(i => new IncidentDto
                    {
                        Classification = i.Classification,
                        PestName = i.PestName,
                        Severity = i.Severity,
                        AffectedCrop = i.AffectedCrop,
                        Status = i.Status,
                        Notes = i.Notes,
                        Treatment = i.Treatment,
                        FieldName = i.FieldName,
                        Confidence = i.Confidence,
                        ReportDate = i.ReportDate,
                        LocationJson = i.Location!.ToJson()
                    }).ToList()
            };

            await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(snap));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] save failed: {ex.Message}");
        }
    }

    public static async Task<(List<Parcel> Parcels, List<Incident> Incidents)> LoadAsync()
    {
        var parcels = new List<Parcel>();
        var incidents = new List<Incident>();

        try
        {
            if (!File.Exists(FilePath)) return (parcels, incidents);

            var snap = JsonSerializer.Deserialize<Snapshot>(
                await File.ReadAllTextAsync(FilePath));
            if (snap is null) return (parcels, incidents);

            foreach (var p in snap.Parcels)
                parcels.Add(new Parcel
                {
                    Name = p.Name,
                    Crop = p.Crop,
                    Acres = p.Acres,
                    MappedDate = p.MappedDate,
                    Geometry = Geometry.FromJson(p.GeometryJson)
                });

            foreach (var i in snap.Incidents)
                incidents.Add(new Incident
                {
                    Classification = i.Classification,
                    PestName = i.PestName,
                    Severity = i.Severity,
                    AffectedCrop = i.AffectedCrop,
                    Status = i.Status,
                    Notes = i.Notes,
                    Treatment = i.Treatment,
                    FieldName = i.FieldName,
                    Confidence = i.Confidence,
                    ReportDate = i.ReportDate,
                    Location = Geometry.FromJson(i.LocationJson) as MapPoint
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] load failed: {ex.Message}");
        }

        return (parcels, incidents);
    }

    public static async Task<(List<Parcel> Parcels, List<Incident> Incidents, List<NeighborFarm> Farms)> LoadDemoPresetAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(DemoPresetFileName);
            var preset = await JsonSerializer.DeserializeAsync<DemoPresetDto>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (preset is null) return (new(), new(), new());

            var parcels = preset.Parcels.Select(parcel => new Parcel
            {
                Name = parcel.Name,
                Crop = parcel.Crop,
                Acres = parcel.Acres,
                MappedDate = parcel.MappedDate,
                Geometry = Geometry.FromJson(parcel.GeometryJson)
            }).Where(parcel => parcel.Geometry is not null).ToList();

            var farms = preset.NeighborFarms.Select(farm => new NeighborFarm
            {
                FarmName = farm.FarmName,
                Owner = farm.Owner,
                Crop = farm.Crop,
                Acres = farm.Acres,
                Location = new MapPoint(farm.Longitude, farm.Latitude, SpatialReferences.Wgs84)
            }).ToList();

            var incidents = preset.Incidents.Select(incident => new Incident
            {
                Classification = incident.Classification,
                PestName = incident.PestName,
                Severity = incident.Severity,
                AffectedCrop = incident.AffectedCrop,
                Status = incident.Status,
                Notes = incident.Notes,
                Treatment = incident.Treatment,
                FieldName = incident.FieldName,
                SpreadAcres = incident.SpreadAcres,
                Confidence = incident.Confidence,
                ReportDate = incident.ReportDate,
                AlertNeighbors = incident.AlertNeighbors,
                AlertRadiusMiles = incident.AlertRadiusMiles,
                AlertedFarmCount = incident.AlertedFarmCount,
                Location = new MapPoint(incident.Longitude, incident.Latitude, SpatialReferences.Wgs84)
            }).ToList();

            return (parcels, incidents, farms);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] demo preset load failed: {ex.Message}");
            return (new(), new(), new());
        }
    }

    /// <summary>Wipes the saved snapshot. Useful for resetting demo state.</summary>
    public static void Reset()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] reset failed: {ex.Message}");
        }
    }

    private sealed class DemoPresetDto
    {
        public List<DemoParcelDto> Parcels { get; set; } = new();
        public List<DemoIncidentDto> Incidents { get; set; } = new();
        public List<DemoFarmDto> NeighborFarms { get; set; } = new();
    }

    private sealed class DemoParcelDto
    {
        public string Name { get; set; } = "";
        public string Crop { get; set; } = "";
        public double Acres { get; set; }
        public DateTime MappedDate { get; set; }
        public string GeometryJson { get; set; } = "";
    }

    private sealed class DemoFarmDto
    {
        public string FarmName { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Crop { get; set; } = "";
        public double Acres { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class DemoIncidentDto
    {
        public string Classification { get; set; } = "";
        public string PestName { get; set; } = "";
        public string Severity { get; set; } = "LOW";
        public string AffectedCrop { get; set; } = "";
        public string Status { get; set; } = "OPEN";
        public string Notes { get; set; } = "";
        public string Treatment { get; set; } = "";
        public string FieldName { get; set; } = "";
        public double SpreadAcres { get; set; }
        public int Confidence { get; set; }
        public DateTime ReportDate { get; set; }
        public bool AlertNeighbors { get; set; }
        public double AlertRadiusMiles { get; set; }
        public int AlertedFarmCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}