using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public class RiskAssessment
{
    public Parcel Parcel { get; set; } = null!;
    public Incident Source { get; set; } = null!;
    public double DistanceMeters { get; set; }
    public string RiskLevel { get; set; } = "LOW";

    public string Summary =>
        $"{Parcel.Name} — {RiskLevel} risk, {DistanceMeters:F0}m from {Source.PestName}";
    public string RiskDetail =>
    $"{RiskLevel} · {DistanceMeters:F0}m from {Source.PestName}";
}

public class RiskResult
{
    public Geometry? ThreatZone { get; set; }
    public List<RiskAssessment> AtRisk { get; } = new();
    public List<Parcel> Infected { get; } = new();
}

public static class RiskEngine
{
    // Spread radius is a function of what the vision model diagnosed.
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
        var buffers = new List<Geometry>();
        var seen = new HashSet<string>();

        foreach (var inc in incidents)
        {
            if (inc.Location is null) continue;

            double radius = RadiusMetersFor(inc.Severity);

            var buffer = GeometryEngine.BufferGeodetic(
                inc.Location, radius, LinearUnits.Meters,
                double.NaN, GeodeticCurveType.Geodesic);

            buffers.Add(buffer);

            foreach (var p in parcels)
            {
                if (p.Geometry is null) continue;

                var bufferSpatialReference = buffer.SpatialReference;
                if (bufferSpatialReference is null) continue;

                var geom = GeometryEngine.Project(p.Geometry, bufferSpatialReference);

                // Field containing the incident is infected, not merely at risk.
                if (GeometryEngine.Intersects(geom, inc.Location))
                {
                    if (!result.Infected.Contains(p)) result.Infected.Add(p);
                    continue;
                }

                if (!GeometryEngine.Intersects(geom, buffer)) continue;
                if (!seen.Add(p.Name)) continue;

                var nearest = GeometryEngine.NearestCoordinate(geom, inc.Location);
                double dist = nearest is null ? radius
                    : GeometryEngine.DistanceGeodetic(
                        inc.Location, nearest.Coordinate, LinearUnits.Meters,
                        AngularUnits.Degrees, GeodeticCurveType.Geodesic).Distance;

                // Closer to the source = higher risk.
                double ratio = dist / radius;
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
        }

        if (buffers.Count > 0)
            result.ThreatZone = GeometryEngine.Union(buffers);

        result.AtRisk.Sort((a, b) => a.DistanceMeters.CompareTo(b.DistanceMeters));
        return result;
    }
}