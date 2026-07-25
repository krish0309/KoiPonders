using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
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
        // Center-pivot irrigation country near Garden City, Kansas.
        private const double InitialLatitude = 37.6420;
        private const double InitialLongitude = -100.8790;
        private const double InitialScale = 40000;

        public MapViewModel()
        {
            _map = new Map(BasemapStyle.ArcGISImagery)
            {
                InitialViewpoint = new Viewpoint(
                    new MapPoint(InitialLongitude, InitialLatitude, SpatialReferences.Wgs84),
                    InitialScale)
            };
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