using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public class Parcel
{
    public string Name { get; set; } = "";
    public double Acres { get; set; }
    public DateTime MappedDate { get; set; }
    public Geometry? Geometry { get; set; }

    public string AcresDisplay => $"{Acres:F1} Ac";
    public string MappedDisplay => $"Mapped {MappedDate:MMM dd, yyyy}";
}