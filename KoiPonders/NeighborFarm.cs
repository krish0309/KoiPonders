using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public class NeighborFarm
{
    public string FarmName { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Crop { get; set; } = "";
    public double Acres { get; set; }
    public MapPoint? Location { get; set; }
}

public class AlertRecipient
{
    public NeighborFarm Farm { get; set; } = null!;
    public double DistanceMiles { get; set; }
    public string Reason { get; set; } = "";

    public string Headline => $"{Farm.FarmName} · {Farm.Owner}";
    public string Detail => $"{DistanceMiles:F1} mi · {Farm.Crop} · {Reason}";
}