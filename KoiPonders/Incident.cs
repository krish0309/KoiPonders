using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public class Incident
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
    public DateTime ReportDate { get; set; } = DateTime.Now;
    public MapPoint? Location { get; set; }
    public byte[]? Photo { get; set; }

    public string DateDisplay => $"{ReportDate:MMM dd, yyyy}";

    public string SeverityColor => Severity switch
    {
        "CRITICAL" => "#DC2626",
        "HIGH" => "#EA580C",
        "MEDIUM" => "#CA8A04",
        _ => "#0891B2"
    };
}