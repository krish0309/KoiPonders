using System.Windows.Input;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using KoiPonders.Mvvm;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Backs the "Report Location" picker: the user taps the map to drop the report pin.
	/// The most important part of a report is exactly where it happened, so this is a
	/// dedicated, focused location-capture surface backed by ArcGIS.
	/// </summary>
	public sealed class ReportLocationViewModel : ObservableObject
	{
		private double? _latitude;
		private double? _longitude;
		private string _statusMessage = "Tap the map where you spotted the problem.";

		public ReportLocationViewModel()
		{
			Map = new Map(BasemapStyle.ArcGISImagery)
			{
				InitialViewpoint = new Viewpoint(new Envelope(-180, -85, 180, 85, SpatialReferences.Wgs84))
			};

			ClearCommand = new RelayCommand(_ => ClearLocation(), _ => HasLocation);
		}

		public Map Map { get; }

		public ICommand ClearCommand { get; }

		public bool HasLocation => _latitude.HasValue && _longitude.HasValue;

		public double? Latitude
		{
			get => _latitude;
			private set
			{
				if (SetProperty(ref _latitude, value))
				{
					OnPropertyChanged(nameof(HasLocation));
					OnPropertyChanged(nameof(CoordinateText));
				}
			}
		}

		public double? Longitude
		{
			get => _longitude;
			private set
			{
				if (SetProperty(ref _longitude, value))
				{
					OnPropertyChanged(nameof(HasLocation));
					OnPropertyChanged(nameof(CoordinateText));
				}
			}
		}

		public string StatusMessage
		{
			get => _statusMessage;
			private set => SetProperty(ref _statusMessage, value);
		}

		public string CoordinateText => HasLocation
			? $"Lat {Latitude:0.00000}, Lon {Longitude:0.00000}"
			: "No location set";

		/// <summary>
		/// Records the tapped location (in WGS84) and updates status text.
		/// </summary>
		public void SetLocation(MapPoint mapPoint)
		{
			if (mapPoint is null)
			{
				return;
			}

			var wgs84 = mapPoint.SpatialReference is { Wkid: 4326 }
				? mapPoint
				: (MapPoint)GeometryEngine.Project(mapPoint, SpatialReferences.Wgs84);

			Latitude = wgs84.Y;
			Longitude = wgs84.X;
			StatusMessage = $"Pin dropped at {CoordinateText}. Tap again to move it.";
			(ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}

		private void ClearLocation()
		{
			Latitude = null;
			Longitude = null;
			StatusMessage = "Tap the map where you spotted the problem.";
			(ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}
	}
}
