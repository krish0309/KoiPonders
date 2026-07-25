using Esri.ArcGISRuntime.Geometry;
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

            mapView.GraphicsOverlays?.Add(_viewModel.ReportsOverlay);
            mapView.GeoViewTapped += OnMapTapped;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadReportsAsync();
        }

        // A report is initiated by tapping the map: tapping an existing pin opens that
        // report for editing, while tapping anywhere else drops a new report at that spot.
        private async void OnMapTapped(object? sender, GeoViewInputEventArgs e)
        {
            e.Handled = true;

            var identify = await mapView.IdentifyGraphicsOverlayAsync(
                _viewModel.ReportsOverlay, e.Position, 12, false, 1);

            var graphic = identify.Graphics.FirstOrDefault();
            if (graphic is not null &&
                graphic.Attributes.TryGetValue("reportId", out var reportIdValue) &&
                reportIdValue is string reportId)
            {
                await Shell.Current.GoToAsync($"{nameof(Views.ReportEditPage)}?reportId={reportId}");
                return;
            }

            if (e.Location is null)
            {
                return;
            }

            var wgs84 = e.Location.SpatialReference is { Wkid: 4326 }
                ? e.Location
                : GeometryEngine.Project(e.Location, SpatialReferences.Wgs84) as MapPoint;

            if (wgs84 is null)
            {
                return;
            }

            var route = $"{nameof(Views.ReportEditPage)}" +
                $"?lat={MapViewModel.FormatCoordinate(wgs84.Y)}" +
                $"&lon={MapViewModel.FormatCoordinate(wgs84.X)}";

            await Shell.Current.GoToAsync(route);
        }
    }
}
