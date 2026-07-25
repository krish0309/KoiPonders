using Esri.ArcGISRuntime.Maui;

namespace KoiPonders
{
	public partial class MainPage : ContentPage
	{
		private readonly MapViewModel _viewModel;

		public MainPage(MapViewModel viewModel)
		{
			InitializeComponent();

			_viewModel = viewModel;
			BindingContext = _viewModel;

			mapView.GraphicsOverlays?.Add(_viewModel.FieldsOverlay);
			mapView.GeometryEditor = _viewModel.GeometryEditor;
			mapView.GeoViewTapped += OnMapTapped;
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			await _viewModel.LoadFieldsAsync();
		}

		private async void OnMapTapped(object? sender, GeoViewInputEventArgs e)
		{
			if (!_viewModel.IsReporting)
			{
				return;
			}

			e.Handled = true;

			var identify = await mapView.IdentifyGraphicsOverlayAsync(
				_viewModel.FieldsOverlay, e.Position, 12, false, 1);

			var graphic = identify.Graphics.FirstOrDefault();
			if (graphic is null ||
				!graphic.Attributes.TryGetValue("fieldId", out var fieldIdValue) ||
				fieldIdValue is not string fieldId)
			{
				_viewModel.SetStatus("That spot is outside your fields. Tap inside a field boundary.");
				return;
			}

			var mapPoint = e.Location;
			var wgs84 = mapPoint?.SpatialReference is { } sr && sr.Wkid != Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84.Wkid
				? Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(mapPoint, Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84) as Esri.ArcGISRuntime.Geometry.MapPoint
				: mapPoint;

			var fieldName = graphic.Attributes.TryGetValue("name", out var n) ? n?.ToString() : "field";
			_viewModel.CancelReportCommand.Execute(null);
			_viewModel.SetStatus($"Logging incident for '{fieldName}'.");

			var route = $"{nameof(Views.LogPestIncidentPage)}?fieldId={fieldId}";
			if (wgs84 is not null)
			{
				route += $"&lat={wgs84.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
						 $"&lon={wgs84.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
			}

			await Shell.Current.GoToAsync(route);
		}
	}
}
