using System.Globalization;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using KoiPonders.Mvvm;
using KoiPonders.Services;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace KoiPonders
{
    /// <summary>
    /// Provides map data to an application and surfaces the saved pest/disease reports as
    /// pins. Reports are initiated by tapping the map, so this view model also exposes the
    /// graphics overlay used to render (and identify) those report pins.
    /// </summary>
    public class MapViewModel : ObservableObject
    {
        private readonly IReportStore _reportStore;

        private readonly SimpleMarkerSymbol _reportPinSymbol = new(
            SimpleMarkerSymbolStyle.Circle,
            System.Drawing.Color.FromArgb(230, 183, 28, 28),
            16)
        {
            Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 2)
        };

        public MapViewModel(IReportStore reportStore)
        {
            _reportStore = reportStore ?? throw new ArgumentNullException(nameof(reportStore));

            _map = new Map(SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(new Envelope(-180, -85, 180, 85, SpatialReferences.Wgs84)),
                Basemap = new Basemap(BasemapStyle.ArcGISStreets)
            };
        }

        private Map _map;

        /// <summary>
        /// Gets or sets the map
        /// </summary>
        public Map Map
        {
            get => _map;
            set => SetProperty(ref _map, value);
        }

        /// <summary>
        /// Overlay that renders a pin for every saved report. The attribute
        /// <c>reportId</c> is used to identify the tapped report.
        /// </summary>
        public GraphicsOverlay ReportsOverlay { get; } = new() { Id = "reports" };

        /// <summary>
        /// Loads all saved reports from the store and refreshes their pins on the map.
        /// </summary>
        public async Task LoadReportsAsync()
        {
            var reports = await _reportStore.GetReportsAsync();

            ReportsOverlay.Graphics.Clear();
            foreach (var report in reports)
            {
                if (!report.HasLocation)
                {
                    continue;
                }

                var location = new MapPoint(report.Longitude!.Value, report.Latitude!.Value, SpatialReferences.Wgs84);
                var graphic = new Graphic(location, _reportPinSymbol);
                graphic.Attributes["reportId"] = report.Id.ToString();
                ReportsOverlay.Graphics.Add(graphic);
            }
        }

        /// <summary>
        /// Formats a WGS84 coordinate value using the invariant culture so it round-trips
        /// safely through Shell navigation query strings.
        /// </summary>
        public static string FormatCoordinate(double value) =>
            value.ToString(CultureInfo.InvariantCulture);
    }
}
