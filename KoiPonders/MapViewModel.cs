using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace KoiPonders
{
    /// <summary>
    /// Provides map data to an application
    /// </summary>
    public class MapViewModel : INotifyPropertyChanged
    {
        private const string CachedImageryFileName = "hopkinton_6in_wide.tif";
        private const double InitialLatitude = 43.2033888;
        private const double InitialLongitude = -71.6331474;
        private const double InitialScale = 1752;

        private Task? _initializationTask;

        public MapViewModel()
        {
            _map = new Map(SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(
                    new MapPoint(InitialLongitude, InitialLatitude, SpatialReferences.Wgs84),
                    InitialScale)
            };
        }

        public Task InitializeAsync() => _initializationTask ??= InitializeCoreAsync();

        private async Task InitializeCoreAsync()
        {
            var localPath = Path.Combine(FileSystem.AppDataDirectory, CachedImageryFileName);
            if (!File.Exists(localPath))
            {
                await using var source = await FileSystem.OpenAppPackageFileAsync(CachedImageryFileName);
                await using var destination = File.Create(localPath);
                await source.CopyToAsync(destination);
            }

            var imageryLayer = new RasterLayer(new Raster(localPath))
            {
                InitialViewpoint = new Viewpoint(37.6420, -100.8790, 40000)

            };
            Map.OperationalLayers.Add(imageryLayer);
            await imageryLayer.LoadAsync();
            Map.MaxExtent = imageryLayer.FullExtent;
        }

        private Map _map;

        /// <summary>
        /// Gets or sets the map
        /// </summary>
        public Map Map
        {
            get => _map;
            set { _map = value; OnPropertyChanged(); }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
                 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}