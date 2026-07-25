using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Maui;
using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class ReportLocationPage : ContentPage
	{
		private readonly ReportLocationViewModel _viewModel;
		private readonly GraphicsOverlay _pinOverlay = new() { Id = "reportPin" };

		private readonly SimpleMarkerSymbol _pinSymbol = new(
			SimpleMarkerSymbolStyle.Circle,
			System.Drawing.Color.FromArgb(230, 183, 28, 28),
			16)
		{
			Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 2)
		};

		public ReportLocationPage(ReportLocationViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = _viewModel = viewModel;
			mapView.GraphicsOverlays?.Add(_pinOverlay);
		}

		private void OnMapTapped(object? sender, GeoViewInputEventArgs e)
		{
			if (e.Location is null)
			{
				return;
			}

			_viewModel.SetLocation(e.Location);

			_pinOverlay.Graphics.Clear();
			_pinOverlay.Graphics.Add(new Graphic(e.Location, _pinSymbol));
		}

		private async void OnConfirmLocation(object? sender, EventArgs e)
		{
			if (!_viewModel.HasLocation)
			{
				return;
			}

			var parameters = new Dictionary<string, object>
			{
				["lat"] = _viewModel.Latitude!.Value,
				["lon"] = _viewModel.Longitude!.Value,
			};

			await Shell.Current.GoToAsync("..", parameters);
		}
	}
}
