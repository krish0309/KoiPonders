using System.Collections.ObjectModel;
using System.Windows.Input;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Editing;
using KoiPonders.Models;
using KoiPonders.Mvvm;
using KoiPonders.Services;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace KoiPonders
{
    /// <summary>
    /// Drives the interactive farm map: displays saved fields, supports tap-to-draw and
    /// GPS field-walk capture of new field polygons, and persists them locally.
    /// </summary>
    public sealed class MapViewModel : ObservableObject
    {
        private readonly IFarmDataStore _dataStore;

        private readonly SimpleFillSymbol _fieldSymbol = new(
            SimpleFillSymbolStyle.Solid,
            System.Drawing.Color.FromArgb(70, 76, 175, 80),
            new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.FromArgb(220, 46, 125, 50), 2));

        private readonly List<MapPoint> _gpsVertices = new();

        private Map _map;
        private bool _isDrawing;
        private bool _isGpsCapture;
        private bool _isReporting;
        private string _statusMessage = "Tap 'Draw field' or 'Walk field' to map a new plot.";
        private string _newFieldName = string.Empty;
        private string _newFieldCrop = string.Empty;

        public MapViewModel(IFarmDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

            _map = new Map(BasemapStyle.ArcGISImagery)
            {
                InitialViewpoint = new Viewpoint(new Envelope(-180, -85, 180, 85, SpatialReferences.Wgs84))
            };

            FieldsOverlay = new GraphicsOverlay { Id = "fields" };
            GeometryEditor = new GeometryEditor();

            StartDrawCommand = new RelayCommand(StartDraw, () => !IsBusy && !IsReporting);
            StartGpsCaptureCommand = new AsyncRelayCommand(StartGpsCaptureAsync, () => !IsBusy && !IsReporting);
            AddGpsPointCommand = new AsyncRelayCommand(AddGpsPointAsync, () => IsGpsCapture);
            UndoVertexCommand = new RelayCommand(UndoVertex, () => IsDrawing || IsGpsCapture);
            SaveFieldCommand = new AsyncRelayCommand(SaveFieldAsync, () => IsDrawing || IsGpsCapture);
			CancelCommand = new RelayCommand(CancelEditing, () => IsDrawing || IsGpsCapture);
			StartReportCommand = new RelayCommand(StartReport, () => !IsBusy && !IsReporting);
			CancelReportCommand = new RelayCommand(CancelReport, () => IsReporting);
		}

        public Map Map
        {
            get => _map;
            set => SetProperty(ref _map, value);
        }

        public GraphicsOverlay FieldsOverlay { get; }

        public GeometryEditor GeometryEditor { get; }

        public ObservableCollection<PlantField> Fields { get; } = new();

        public bool IsDrawing
        {
            get => _isDrawing;
            private set
            {
                if (SetProperty(ref _isDrawing, value))
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(IsEditing));
                    RaiseCommandStates();
                }
            }
        }

        public bool IsGpsCapture
        {
            get => _isGpsCapture;
            private set
            {
                if (SetProperty(ref _isGpsCapture, value))
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(IsEditing));
                    RaiseCommandStates();
                }
            }
        }

        public bool IsEditing => IsDrawing || IsGpsCapture;

        public bool IsBusy => IsDrawing || IsGpsCapture;

		public bool IsReporting
		{
			get => _isReporting;
			private set
			{
				if (SetProperty(ref _isReporting, value))
				{
					RaiseCommandStates();
				}
			}
		}
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

		/// <summary>
		/// Updates the status banner from outside the view model (e.g. map tap handling).
		/// </summary>
		public void SetStatus(string message) => StatusMessage = message;
        public string NewFieldName
        {
            get => _newFieldName;
            set => SetProperty(ref _newFieldName, value);
        }

        public string NewFieldCrop
        {
            get => _newFieldCrop;
            set => SetProperty(ref _newFieldCrop, value);
        }

        public ICommand StartDrawCommand { get; }

        public ICommand StartGpsCaptureCommand { get; }

        public ICommand AddGpsPointCommand { get; }

        public ICommand UndoVertexCommand { get; }

        public ICommand SaveFieldCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand StartReportCommand { get; }

        public ICommand CancelReportCommand { get; }

        /// <summary>
        /// Loads saved fields from storage and renders them on the map overlay.
        /// </summary>
        public async Task LoadFieldsAsync()
        {
            var fields = await _dataStore.GetFieldsAsync().ConfigureAwait(true);

            Fields.Clear();
            FieldsOverlay.Graphics.Clear();

            foreach (var field in fields)
            {
                Fields.Add(field);

                if (string.IsNullOrWhiteSpace(field.GeometryJson))
                {
                    continue;
                }

                try
                {
                    var geometry = Geometry.FromJson(field.GeometryJson);
                    var graphic = new Graphic(geometry, _fieldSymbol);
                    graphic.Attributes["fieldId"] = field.Id.ToString();
                    graphic.Attributes["name"] = field.Name;
                    FieldsOverlay.Graphics.Add(graphic);
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException)
                {
                    // Skip fields with unreadable geometry rather than failing the whole load.
                }
            }

            StatusMessage = Fields.Count == 0
                ? "No fields yet. Draw or walk your first plot to get started."
                : $"{Fields.Count} field(s) loaded.";
        }

        private void StartDraw()
        {
            ResetEditingState();
            IsGpsCapture = false;
            IsDrawing = true;
            if (!GeometryEditor.IsStarted)
            {
                GeometryEditor.Start(GeometryType.Polygon);
            }

            StatusMessage = "Tap the map to add vertices, then Save.";
        }

        private async Task StartGpsCaptureAsync()
        {
            if (!await EnsureLocationPermissionAsync().ConfigureAwait(true))
            {
                StatusMessage = "Location permission is required to walk a field.";
                return;
            }

            ResetEditingState();
            IsDrawing = false;
            _gpsVertices.Clear();
            IsGpsCapture = true;
            StatusMessage = "Walk the boundary and tap 'Add point' at each corner.";
        }

        private async Task AddGpsPointAsync()
        {
            try
            {
                var location = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15)))
                    .ConfigureAwait(true);

                if (location is null)
                {
                    StatusMessage = "Could not get a GPS fix. Try again.";
                    return;
                }

                var point = new MapPoint(location.Longitude, location.Latitude, SpatialReferences.Wgs84);
                _gpsVertices.Add(point);
                StatusMessage = $"Captured {_gpsVertices.Count} point(s).";
            }
            catch (Exception ex) when (ex is FeatureNotSupportedException or PermissionException)
            {
                StatusMessage = "GPS is not available on this device.";
            }
        }

        private void UndoVertex()
        {
            if (IsGpsCapture && _gpsVertices.Count > 0)
            {
                _gpsVertices.RemoveAt(_gpsVertices.Count - 1);
                StatusMessage = $"Captured {_gpsVertices.Count} point(s).";
            }
            else if (IsDrawing && GeometryEditor.CanUndo)
            {
                GeometryEditor.Undo();
            }
        }

        private async Task SaveFieldAsync()
        {
            Geometry? geometry = BuildGeometry();
            if (geometry is null)
            {
                StatusMessage = "A field needs at least 3 points.";
                return;
            }

            var normalized = GeometryEngine.NormalizeCentralMeridian(geometry) ?? geometry;
            var area = Math.Abs(GeometryEngine.AreaGeodetic(
                normalized,
                AreaUnits.SquareMeters,
                GeodeticCurveType.Geodesic)) / 10_000d;

            var field = new PlantField
            {
                Name = string.IsNullOrWhiteSpace(NewFieldName) ? $"Field {Fields.Count + 1}" : NewFieldName.Trim(),
                CropType = NewFieldCrop.Trim(),
                GeometryJson = normalized.ToJson(),
                AreaHectares = Math.Round(area, 3)
            };

            await _dataStore.UpsertFieldAsync(field).ConfigureAwait(true);

            CancelEditing();
            NewFieldName = string.Empty;
            NewFieldCrop = string.Empty;
            await LoadFieldsAsync().ConfigureAwait(true);
            StatusMessage = $"Saved '{field.Name}' ({field.AreaHectares:0.###} ha).";
        }

        private Geometry? BuildGeometry()
        {
            if (IsGpsCapture)
            {
                if (_gpsVertices.Count < 3)
                {
                    return null;
                }

                return new Polygon(_gpsVertices, SpatialReferences.Wgs84);
            }

            if (!GeometryEditor.IsStarted)
            {
                return null;
            }

            var geometry = GeometryEditor.Stop();
            if (geometry is Polygon polygon && polygon.Parts.Sum(p => p.PointCount) >= 3)
            {
                return polygon;
            }

            return null;
        }

        private void CancelEditing()
        {
            ResetEditingState();
            IsDrawing = false;
            IsGpsCapture = false;
			StatusMessage = "Editing cancelled.";
		}

		private void StartReport()
		{
			if (Fields.Count == 0)
			{
				StatusMessage = "Add a field first, then tap inside it to report.";
				return;
			}

			IsReporting = true;
			StatusMessage = "Report mode: tap inside a field to log an incident there.";
		}

		private void CancelReport()
		{
			IsReporting = false;
			StatusMessage = "Report cancelled.";
		}

        private void ResetEditingState()
        {
            if (GeometryEditor.IsStarted)
            {
                GeometryEditor.Stop();
            }

            _gpsVertices.Clear();
        }

        private static async Task<bool> EnsureLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(true);
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>().ConfigureAwait(true);
            }

            return status == PermissionStatus.Granted;
        }

        private void RaiseCommandStates()
        {
            (StartDrawCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StartGpsCaptureCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (AddGpsPointCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (UndoVertexCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveFieldCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
			(CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(StartReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CancelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}
    }
}
