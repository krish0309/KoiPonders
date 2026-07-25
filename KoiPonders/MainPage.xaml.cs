using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Editing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using KoiPonders.Models;
using KoiPonders.Services;
using Microsoft.Extensions.DependencyInjection;
using Color = System.Drawing.Color;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace KoiPonders;

public partial class MainPage : ContentPage
{
    private readonly MapViewModel _viewModel;
    private readonly GeometryEditor _geometryEditor = new();
    private readonly GraphicsOverlay _parcelOverlay = new();
    private readonly GraphicsOverlay _threatOverlay = new();
    private readonly GraphicsOverlay _riskOverlay = new();
    private readonly GraphicsOverlay _incidentOverlay = new();

    private readonly ObservableCollection<Parcel> _parcels = new();
    private readonly ObservableCollection<Incident> _incidents = new();

    private readonly IReportStore? _reportStore;

    private bool _awaitingIncidentTap;
    private bool _loaded;

    private readonly SimpleFillSymbol _parcelSymbol = new(
        SimpleFillSymbolStyle.Solid,
        Color.FromArgb(70, 34, 197, 94),
        new SimpleLineSymbol(SimpleLineSymbolStyle.Solid,
                             Color.FromArgb(230, 74, 222, 128), 2));

    public MainPage()
    {
        InitializeComponent();
        _viewModel = new MapViewModel();
        BindingContext = _viewModel;

        // Optional — the reporting feature may not be registered.
        _reportStore = MauiProgram.Services?.GetService<IReportStore>();

        mapView.GraphicsOverlays ??= new GraphicsOverlayCollection();
        mapView.GraphicsOverlays.Add(_parcelOverlay);
        mapView.GraphicsOverlays.Add(_threatOverlay);
        mapView.GraphicsOverlays.Add(_riskOverlay);
        mapView.GraphicsOverlays.Add(_incidentOverlay);

        mapView.GeometryEditor = _geometryEditor;
        mapView.GeoViewTapped += OnMapTapped;

        _geometryEditor.PropertyChanged += OnEditorPropertyChanged;

        ParcelList.ItemsSource = _parcels;
        IncidentList.ItemsSource = _incidents;
        SizeChanged += OnPageSizeChanged;
    }

    // ---------- responsive layout ----------

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        var usePhoneLayout = Width > 0 && Width < 700;

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            MapPanel.RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ];
            Microsoft.Maui.Controls.Grid.SetRow(ToolbarScroll, 0);
            Microsoft.Maui.Controls.Grid.SetRow(mapView, 1);
            Microsoft.Maui.Controls.Grid.SetRow(LiveAreaPanel, 1);
            Microsoft.Maui.Controls.Grid.SetRow(StatusPanel, 1);
            Microsoft.Maui.Controls.Grid.SetRow(ThreatPanel, 1);
            ToolbarScroll.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            MapPanel.RowDefinitions = [new RowDefinition(GridLength.Star)];
            Microsoft.Maui.Controls.Grid.SetRow(ToolbarScroll, 0);
            Microsoft.Maui.Controls.Grid.SetRow(mapView, 0);
            Microsoft.Maui.Controls.Grid.SetRow(LiveAreaPanel, 0);
            Microsoft.Maui.Controls.Grid.SetRow(StatusPanel, 0);
            Microsoft.Maui.Controls.Grid.SetRow(ThreatPanel, 0);
            ToolbarScroll.Margin = new Thickness(12);
        }

        if (usePhoneLayout)
        {
            RootGrid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];
            RootGrid.RowDefinitions =
            [
                new RowDefinition(new GridLength(3, GridUnitType.Star)),
                new RowDefinition(new GridLength(2, GridUnitType.Star))
            ];
            Microsoft.Maui.Controls.Grid.SetColumn(MapPanel, 0);
            Microsoft.Maui.Controls.Grid.SetRow(MapPanel, 0);
            Microsoft.Maui.Controls.Grid.SetColumn(ParcelPanel, 0);
            Microsoft.Maui.Controls.Grid.SetRow(ParcelPanel, 1);
            MapPanel.Margin = new Thickness(8, 8, 8, 0);
            ParcelPanel.Padding = new Thickness(12, 10);
        }
        else
        {
            RootGrid.ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(340))
            ];
            RootGrid.RowDefinitions = [new RowDefinition(GridLength.Star)];
            Microsoft.Maui.Controls.Grid.SetColumn(MapPanel, 0);
            Microsoft.Maui.Controls.Grid.SetRow(MapPanel, 0);
            Microsoft.Maui.Controls.Grid.SetColumn(ParcelPanel, 1);
            Microsoft.Maui.Controls.Grid.SetRow(ParcelPanel, 0);
            MapPanel.Margin = new Thickness(16);
            ParcelPanel.Padding = new Thickness(20, 24);
        }
    }

    // ---------- startup ----------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            StatusLabel.Text = "Loading local farm imagery…";
            await _viewModel.InitializeAsync();
            StatusLabel.Text = "WGS84 • EPSG:3857";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "WGS84 • EPSG:3857";
            System.Diagnostics.Debug.WriteLine($"[Imagery] {ex.Message}");
        }

        if (!_loaded)
        {
            _loaded = true;

            var (parcels, _) = await FarmStore.LoadAsync();

            foreach (var parcel in parcels)
            {
                _parcels.Add(parcel);
                if (parcel.Geometry is null) continue;

                _parcelOverlay.Graphics.Add(new Graphic(parcel.Geometry, _parcelSymbol));

                var label = new TextSymbol(
                    $"{parcel.Name}  {parcel.Acres:F1} Ac", Color.White, 11,
                    Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                    Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle)
                {
                    HaloColor = Color.FromArgb(190, 10, 30, 20),
                    HaloWidth = 2
                };
                if (parcel.Geometry.Extent is { } extent)
                    _parcelOverlay.Graphics.Add(new Graphic(extent.GetCenter(), label));
            }

            ParcelCountLabel.Text = $"{_parcels.Count} Total";

            if (_parcels.FirstOrDefault()?.Geometry is { } geometry)
                await mapView.SetViewpointGeometryAsync(geometry, 120);
        }

        await LoadIncidentsAsync();
    }

    /// <summary>
    /// Rebuilds pins and risk analysis from BOTH sources: incidents saved by the
    /// AI report flow, and reports saved by the form-based report store.
    /// </summary>
    private async Task LoadIncidentsAsync()
    {
        _incidents.Clear();
        _incidentOverlay.Graphics.Clear();

        var (_, saved) = await FarmStore.LoadAsync();
        foreach (var inc in saved)
        {
            _incidents.Add(inc);
            DrawIncident(inc);
        }

        if (_reportStore is not null)
        {
            var reports = await _reportStore.GetReportsAsync();

            foreach (var report in reports)
            {
                if (!report.HasLocation) continue;

                var location = new MapPoint(
                    report.Longitude!.Value, report.Latitude!.Value, SpatialReferences.Wgs84);

                var incident = new Incident
                {
                    PestName = report.ProblemName,
                    Classification = report.Category.ToString(),
                    Severity = MapSeverity(report.Severity),
                    Status = report.Status.ToString(),
                    Notes = report.Notes,
                    ReportDate = report.ObservedUtc.LocalDateTime,
                    Location = location,
                    FieldName = FieldNameAt(location)
                };

                _incidents.Add(incident);
                DrawIncident(incident);
            }
        }

        RunRiskAnalysis();

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = $"{_incidents.Count} Total";
    }

    private static string MapSeverity(Models.Severity severity) => severity switch
    {
        Models.Severity.Critical => "CRITICAL",
        Models.Severity.High => "HIGH",
        Models.Severity.Moderate => "MEDIUM",
        _ => "LOW"
    };

    private string FieldNameAt(MapPoint location) =>
        _parcels.FirstOrDefault(p =>
            p.Geometry is not null &&
            GeometryEngine.Intersects(
                GeometryEngine.Project(p.Geometry, location.SpatialReference),
                location))?.Name ?? "Unassigned";

    // ---------- tools ----------

    private void OnPolygonToolClicked(object sender, EventArgs e)
    {
        _geometryEditor.Tool = new VertexTool();
        StartDrawing();
    }

    private void OnFreehandToolClicked(object sender, EventArgs e)
    {
        _geometryEditor.Tool = new FreehandTool();
        StartDrawing();
    }

    private void StartDrawing()
    {
        if (!_geometryEditor.IsStarted)
            _geometryEditor.Start(GeometryType.Polygon);

        ConfirmButton.IsEnabled = true;
    }

    private void OnUndoClicked(object sender, EventArgs e)
    {
        if (_geometryEditor.CanUndo)
            _geometryEditor.Undo();
    }

    // ---------- live acreage ----------

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GeometryEditor.Geometry)) return;

        if (_geometryEditor.Geometry is Polygon poly && !poly.IsEmpty)
        {
            double acres = Math.Abs(GeometryEngine.AreaGeodetic(
                poly, AreaUnits.Acres, GeodeticCurveType.Geodesic));

            LiveAreaLabel.Text = $"{acres:F1} Ac";
            LiveAreaPanel.IsVisible = acres > 0;
        }
        else
        {
            LiveAreaPanel.IsVisible = false;
        }
    }

    // ---------- confirm ----------

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (!_geometryEditor.IsStarted) return;

        var geometry = _geometryEditor.Stop();
        ConfirmButton.IsEnabled = false;
        LiveAreaPanel.IsVisible = false;

        if (geometry is not Polygon polygon || polygon.IsEmpty ||
            polygon.Parts.Count == 0 || polygon.Parts[0].PointCount < 3)
        {
            await DisplayAlertAsync("Not enough points",
                "Tap at least three points to close a field boundary.", "OK");
            return;
        }

        string fallback = $"Field {_parcels.Count + 1}";
        string name = await DisplayPromptAsync(
            "Name this field",
            "e.g. North Cornfield Block A",
            initialValue: fallback) ?? fallback;

        if (string.IsNullOrWhiteSpace(name)) name = fallback;

        double acres = Math.Abs(GeometryEngine.AreaGeodetic(
            polygon, AreaUnits.Acres, GeodeticCurveType.Geodesic));

        _parcelOverlay.Graphics.Add(new Graphic(polygon, _parcelSymbol));

        var labelSymbol = new TextSymbol(
            $"{name}  {acres:F1} Ac", Color.White, 11,
            Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
            Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle)
        {
            HaloColor = Color.FromArgb(190, 10, 30, 20),
            HaloWidth = 2
        };
        _parcelOverlay.Graphics.Add(
            new Graphic(polygon.Extent.GetCenter(), labelSymbol));

        _parcels.Insert(0, new Parcel
        {
            Name = name,
            Acres = acres,
            MappedDate = DateTime.Now,
            Geometry = polygon
        });

        ParcelCountLabel.Text = $"{_parcels.Count} Total";

        if (_incidents.Count > 0) RunRiskAnalysis();
        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    // ---------- zoom to parcel ----------

    private async void OnParcelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Parcel p && p.Geometry is not null)
            await mapView.SetViewpointGeometryAsync(p.Geometry, 60);
    }

    // ---------- incident reporting ----------

    private void OnReportIncidentClicked(object sender, EventArgs e)
    {
        if (_geometryEditor.IsStarted) _geometryEditor.Stop();

        _awaitingIncidentTap = true;
        StatusLabel.Text = "Tap the map where the problem was observed";
    }

    private async void OnMapTapped(object? sender,
        Esri.ArcGISRuntime.Maui.GeoViewInputEventArgs e)
    {
        if (!_awaitingIncidentTap || e.Location is null) return;

        _awaitingIncidentTap = false;
        StatusLabel.Text = "WGS84 • EPSG:3857";

        var page = new IncidentReportPage(e.Location);
        await Navigation.PushModalAsync(page);

        var incident = await page.Result;
        if (incident is null) return;

        incident.FieldName = FieldNameAt(e.Location);

        _incidents.Add(incident);
        DrawIncident(incident);
        RunRiskAnalysis();

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = $"{_incidents.Count} Total";

        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    private void DrawIncident(Incident inc)
    {
        if (inc.Location is null) return;

        var color = inc.Severity switch
        {
            "CRITICAL" => Color.FromArgb(255, 220, 38, 38),
            "HIGH" => Color.FromArgb(255, 234, 88, 12),
            "MEDIUM" => Color.FromArgb(255, 202, 138, 4),
            _ => Color.FromArgb(255, 8, 145, 178)
        };

        var symbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, color, 16)
        {
            Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.White, 2)
        };

        _incidentOverlay.Graphics.Add(new Graphic(inc.Location, symbol));
    }

    // ---------- spatial risk propagation ----------

    private void RunRiskAnalysis()
    {
        _threatOverlay.Graphics.Clear();
        _riskOverlay.Graphics.Clear();

        var result = RiskEngine.Analyze(_incidents, _parcels);

        if (result.ThreatZone is not null)
        {
            var zoneSymbol = new SimpleFillSymbol(
                SimpleFillSymbolStyle.Solid,
                Color.FromArgb(45, 245, 158, 11),
                new SimpleLineSymbol(SimpleLineSymbolStyle.Dash,
                                     Color.FromArgb(200, 245, 158, 11), 2));

            _threatOverlay.Graphics.Add(new Graphic(result.ThreatZone, zoneSymbol));
        }

        var riskSymbol = new SimpleFillSymbol(
            SimpleFillSymbolStyle.Solid,
            Color.FromArgb(80, 245, 158, 11),
            new SimpleLineSymbol(SimpleLineSymbolStyle.Solid,
                                 Color.FromArgb(255, 217, 119, 6), 3));

        foreach (var r in result.AtRisk)
            if (r.Parcel.Geometry is not null)
                _riskOverlay.Graphics.Add(new Graphic(r.Parcel.Geometry, riskSymbol));

        RiskList.ItemsSource = result.AtRisk;

        double exposed = result.AtRisk.Sum(r => r.Parcel.Acres);
        ThreatSummaryLabel.Text =
            $"{_incidents.Count} active incident(s). " +
            $"{result.AtRisk.Count} neighboring field(s) within spread radius — " +
            $"{exposed:F1} acres exposed.";

        ThreatPanel.IsVisible = result.AtRisk.Count > 0 || result.Infected.Count > 0;
    }

    // ---------- panel controls ----------

    private void OnCloseThreatPanel(object sender, EventArgs e)
    {
        ThreatPanel.IsVisible = false;
    }

    private async void OnClearIncidents(object sender, EventArgs e)
    {
        if (_reportStore is not null)
        {
            var reports = await _reportStore.GetReportsAsync();
            foreach (var report in reports)
                await _reportStore.DeleteReportAsync(report.Id);
        }

        _incidents.Clear();
        _incidentOverlay.Graphics.Clear();
        _threatOverlay.Graphics.Clear();
        _riskOverlay.Graphics.Clear();
        RiskList.ItemsSource = null;
        ThreatPanel.IsVisible = false;

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = "0 Total";

        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    // ---------- panel tabs ----------

    private void OnShowParcels(object sender, EventArgs e)
    {
        ParcelList.IsVisible = true;
        RecordsContainer.IsVisible = false;
        PanelTitleLabel.Text = "Mapped Parcels";
        ParcelCountLabel.Text = $"{_parcels.Count} Total";
        AddBoundaryButton.IsVisible = true;

        ParcelsTabButton.BackgroundColor = MauiColor.FromArgb("#15803D");
        ParcelsTabButton.TextColor = Colors.White;
        RecordsTabButton.BackgroundColor = MauiColor.FromArgb("#DDE8F0");
        RecordsTabButton.TextColor = MauiColor.FromArgb("#5A6E77");
    }

    private void OnShowRecords(object sender, EventArgs e)
    {
        ParcelList.IsVisible = false;
        RecordsContainer.IsVisible = true;
        PanelTitleLabel.Text = "Records & Log";
        ParcelCountLabel.Text = $"{_incidents.Count} Total";
        AddBoundaryButton.IsVisible = false;

        RecordsTabButton.BackgroundColor = MauiColor.FromArgb("#15803D");
        RecordsTabButton.TextColor = Colors.White;
        ParcelsTabButton.BackgroundColor = MauiColor.FromArgb("#DDE8F0");
        ParcelsTabButton.TextColor = MauiColor.FromArgb("#5A6E77");
    }

    private async void OnIncidentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Incident inc) return;

        if (inc.Location is not null)
            await mapView.SetViewpointCenterAsync(inc.Location, 12000);

        if (!string.IsNullOrWhiteSpace(inc.Treatment))
            await DisplayAlertAsync($"{inc.PestName} — {inc.Severity}",
                $"{inc.Notes}\n\nRecommended action:\n{inc.Treatment}", "Close");
    }

    private async void OnGenerateSummary(object sender, EventArgs e)
    {
        if (_incidents.Count == 0)
        {
            AiSummaryLabel.Text = "No incidents logged yet.";
            return;
        }

        SummaryButton.IsEnabled = false;
        AiSummaryLabel.Text = "Analyzing incident history…";

        AiSummaryLabel.Text = await PestClassifier.SummarizeAsync(_incidents);

        SummaryButton.IsEnabled = true;
    }

    // ---------- temporary AI test (delete before the demo) ----------

    private async void OnTestAiClicked(object sender, EventArgs e)
    {
        var photo = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "Select crop photo"
        });
        if (photo is null) return;

        using var stream = await photo.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var result = await PestClassifier.ClassifyAsync(ms.ToArray());

        await DisplayAlertAsync("Result",
            result is null ? "Failed — check Output window"
                           : $"{result.PestName}\n{result.Severity} · {result.Confidence}%\n\n{result.Notes}",
            "OK");
    }
}