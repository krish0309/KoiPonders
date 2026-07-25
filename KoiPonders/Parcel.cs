using Esri.ArcGISRuntime.Geometry;
using KoiPonders.Mvvm;

namespace KoiPonders;

public class Parcel : ObservableObject
{
    private string _name = "";
    private string _crop = "";
    private double _acres;
    private DateTime _mappedDate;
    private bool _isExpanded;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(AcresDisplay));
        }
    }

    public string Crop
    {
        get => _crop;
        set => SetProperty(ref _crop, value);
    }

    public double Acres
    {
        get => _acres;
        set
        {
            if (SetProperty(ref _acres, value))
                OnPropertyChanged(nameof(AcresDisplay));
        }
    }

    public DateTime MappedDate
    {
        get => _mappedDate;
        set
        {
            if (SetProperty(ref _mappedDate, value))
                OnPropertyChanged(nameof(MappedDisplay));
        }
    }

    public Geometry? Geometry { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string AcresDisplay => $"{Acres:F1} Ac";
    public string MappedDisplay => $"Mapped {MappedDate:MMM dd, yyyy}";
}