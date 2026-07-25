using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public class RiskAssessment
{
    public Parcel Parcel { get; set; } = null!;
    public Incident Source { get; set; } = null!;
    public double DistanceMeters { get; set; }
    public string RiskLevel { get; set; } = "LOW";

    public string RiskDetail =>
        $"{RiskLevel} · {DistanceMeters:F0}m from {Source.PestName}";

    public string Summary =>
        $"{Parcel.Name} — {RiskLevel} risk, {DistanceMeters:F0}m from {Source.PestName}";
}

public class RiskResult
{
    public Geometry? ThreatZone { get; set; }
    public List<RiskAssessment> AtRisk { get; } = new();
    public List<Parcel> Infected { get; } = new();
}

public static class RiskEngine
{
    // Spread radius is a function of what the AI diagnosed.
    // This is the line where AI output becomes geography.
    public static double RadiusMetersFor(string severity) => severity switch
    {
        "CRITICAL" => 2000,
        "HIGH" => 1200,
        "MEDIUM" => 600,
        _ => 250
    };

    public static RiskResult Analyze(IEnumerable<Incident> incidents,
                                     IEnumerable<Parcel> parcels)
    {
        var result = new RiskResult();

        var incidentList = incidents.Where(i => i.Location is not null).ToList();
        var parcelList = parcels.Where(p => p.Geometry is not null).ToList();

        if (incidentList.Count == 0) return result;

        // Everything gets normalized to one spatial reference before any
        // geometry op — incidents arrive from several sources with different SRs.
        var target =
            parcelList.FirstOrDefault(p => p.Geometry!.SpatialReference is not null)
                      ?.Geometry!.SpatialReference
            ?? incidentList.FirstOrDefault(i => i.Location!.SpatialReference is not null)
                      ?.Location!.SpatialReference
            ?? SpatialReferences.WebMercator;

        var buffers = new List<Geometry>();
        var seen = new HashSet<string>();

        foreach (var inc in incidentList)
        {
            var point = Normalize(inc.Location!, target) as MapPoint;
            if (point is null) continue;

            double radius = RadiusMetersFor(inc.Severity);

            Geometry buffer;
            try
            {
                buffer = GeometryEngine.BufferGeodetic(
                    point, radius, LinearUnits.Meters,
                    double.NaN, GeodeticCurveType.Geodesic);
            }
            catch
            {
                continue;
            }

            buffers.Add(buffer);

            foreach (var p in parcelList)
            {
                var geom = Normalize(p.Geometry!, target);
                if (geom is null) continue;

                try
                {
                    // Field containing the incident is infected, not merely at risk.
                    if (GeometryEngine.Intersects(geom, point))
                    {
                        if (!result.Infected.Contains(p)) result.Infected.Add(p);
                        continue;
                    }

                    if (!GeometryEngine.Intersects(geom, buffer)) continue;
                    if (!seen.Add(p.Name)) continue;

                    var nearest = GeometryEngine.NearestCoordinate(geom, point);
                    double dist = nearest is null ? radius
                        : GeometryEngine.DistanceGeodetic(
                            point, nearest.Coordinate, LinearUnits.Meters,
                            AngularUnits.Degrees, GeodeticCurveType.Geodesic).Distance;

                    // Closer to the source = higher risk.
                    double ratio = radius <= 0 ? 1 : dist / radius;
                    string level = ratio switch
                    {
                        < 0.25 => "CRITICAL",
                        < 0.50 => "HIGH",
                        < 0.75 => "MEDIUM",
                        _ => "LOW"
                    };

                    result.AtRisk.Add(new RiskAssessment
                    {
                        Parcel = p,
                        Source = inc,
                        DistanceMeters = dist,
                        RiskLevel = level
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RiskEngine] {p.Name}: {ex.Message}");
                }
            }
        }

        if (buffers.Count > 0)
        {
            try { result.ThreatZone = GeometryEngine.Union(buffers); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RiskEngine] union failed: {ex.Message}");
                result.ThreatZone = buffers[0];
            }
        }

        result.AtRisk.Sort((a, b) => a.DistanceMeters.CompareTo(b.DistanceMeters));
        return result;
    }

    private static Geometry? Normalize(Geometry geometry, SpatialReference target)
    {
        try
        {
            if (geometry.SpatialReference is null) return null;
            if (geometry.SpatialReference.Wkid == target.Wkid) return geometry;
            return GeometryEngine.Project(geometry, target);
        }
        catch
        {
            return null;
        }
    }
}