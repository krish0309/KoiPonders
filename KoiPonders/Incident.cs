using Esri.ArcGISRuntime.Geometry;
using KoiPonders.Mvvm;

namespace KoiPonders;

public class Incident : ObservableObject
{
    private string _classification = "";
    private string _pestName = "";
    private string _severity = "LOW";
    private string _affectedCrop = "";
    private string _status = "OPEN";
    private string _notes = "";
    private string _fieldName = "";
    private bool _isExpanded;

    public string Classification
    {
        get => _classification;
        set => SetProperty(ref _classification, value);
    }

    public string PestName
    {
        get => _pestName;
        set => SetProperty(ref _pestName, value);
    }

    public string Severity
    {
        get => _severity;
        set
        {
            if (SetProperty(ref _severity, value))
                OnPropertyChanged(nameof(SeverityColor));
        }
    }

    public string AffectedCrop
    {
        get => _affectedCrop;
        set => SetProperty(ref _affectedCrop, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string FieldName
    {
        get => _fieldName;
        set => SetProperty(ref _fieldName, value);
    }

    public double SpreadAcres { get; set; }
    public int Confidence { get; set; }
    public DateTime ReportDate { get; set; } = DateTime.Now;
    public MapPoint? Location { get; set; }
    public byte[]? Photo { get; set; }
    public Guid ReportId { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string DateDisplay => $"{ReportDate:MMM dd, yyyy}";

    public string SeverityColor => Severity switch
    {
        "CRITICAL" => "#DC2626",
        "HIGH" => "#EA580C",
        "MEDIUM" => "#CA8A04",
        _ => "#0891B2"
    };
}