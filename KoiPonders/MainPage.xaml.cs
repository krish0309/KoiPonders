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
    private const string MenuIcon = "\u2630";
    private const string AddIcon = "+";
    private const string CloseIcon = "\u00D7";

    private readonly MapViewModel _viewModel;
    private readonly GeometryEditor _geometryEditor = new();
    private readonly GraphicsOverlay _parcelOverlay = new();
    private readonly GraphicsOverlay _threatOverlay = new();
    private readonly GraphicsOverlay _riskOverlay = new();
    private readonly GraphicsOverlay _neighborOverlay = new();
    private readonly GraphicsOverlay _incidentOverlay = new();
    private readonly GraphicsOverlay _spreadOverlay = new();

    private readonly ObservableCollection<Parcel> _parcels = new();
    private readonly ObservableCollection<Incident> _incidents = new();

    // Maps drawn graphics back to their source data so a map tap can surface attributes.
    private readonly Dictionary<Graphic, Incident> _incidentGraphics = new();
    private readonly Dictionary<Graphic, Parcel> _parcelGraphics = new();
    private readonly Dictionary<Graphic, NeighborFarm> _neighborGraphics = new();

    private readonly IReportStore _reportStore;

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

        // Optional - the reporting feature may not be registered.
        _reportStore = MauiProgram.Services.GetRequiredService<IReportStore>();

        mapView.GraphicsOverlays ??= new GraphicsOverlayCollection();
        mapView.GraphicsOverlays.Add(_parcelOverlay);
        mapView.GraphicsOverlays.Add(_threatOverlay);
        mapView.GraphicsOverlays.Add(_riskOverlay);
        mapView.GraphicsOverlays.Add(_neighborOverlay);
        mapView.GraphicsOverlays.Add(_incidentOverlay);
        mapView.GraphicsOverlays.Add(_spreadOverlay);

        mapView.GeometryEditor = _geometryEditor;
        mapView.GeoViewTapped += OnMapTapped;

        _geometryEditor.PropertyChanged += OnEditorPropertyChanged;

        ParcelList.ItemsSource = _parcels;
        IncidentList.ItemsSource = _incidents;
        SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;

        var useCompactLayout = Width < 700;
        ParcelPanel.WidthRequest = useCompactLayout ? Math.Max(280, Width - 24) : 380;
        ParcelPanel.Margin = useCompactLayout
            ? new Thickness(12, 12, 12, 92)
            : new Thickness(16);

        ThreatPanel.WidthRequest = useCompactLayout ? Math.Max(260, Width - 32) : 310;
    }

    private async void OnToggleActionMenu(object? sender, EventArgs e)
    {
        if (ParcelPanel.IsVisible)
            await HideWorkspacePanelAsync();

        var showMenu = !ActionMenu.IsVisible;
        ActionMenu.IsVisible = showMenu;
        ActionMenuButton.Text = showMenu ? CloseIcon : AddIcon;
        SemanticProperties.SetDescription(ActionMenuButton, showMenu ? "Close map actions" : "Open map actions");
    }

    private void CloseActionMenu()
    {
        ActionMenu.IsVisible = false;
        ActionMenuButton.Text = AddIcon;
        SemanticProperties.SetDescription(ActionMenuButton, "Open map actions");
    }

    private async void OnTogglePanelClicked(object sender, EventArgs e)
    {
        CloseActionMenu();

        if (ParcelPanel.IsVisible)
            await HideWorkspacePanelAsync();
        else
            await ShowWorkspacePanelAsync();
    }

    private async void OnClosePanelClicked(object? sender, EventArgs e)
    {
        await HideWorkspacePanelAsync();
    }

    private async Task ShowWorkspacePanelAsync()
    {
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            BottomActionControls.IsVisible = false;

        ParcelPanel.TranslationX = ParcelPanel.WidthRequest + 32;
        ParcelPanel.IsVisible = true;
        PanelToggleButton.Text = CloseIcon;
        SemanticProperties.SetDescription(PanelToggleButton, "Close parcels and records");
        await ParcelPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut);
    }

    private async Task HideWorkspacePanelAsync()
    {
        if (!ParcelPanel.IsVisible) return;

        await ParcelPanel.TranslateToAsync(ParcelPanel.WidthRequest + 32, 0, 180, Easing.CubicIn);
        ParcelPanel.IsVisible = false;
        BottomActionControls.IsVisible = true;
        PanelToggleButton.Text = MenuIcon;
        SemanticProperties.SetDescription(PanelToggleButton, "Open parcels and records");
    }

    // ---------- startup ----------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Imagery] {ex.Message}");
        }

        if (!_loaded)
        {
            _loaded = true;

#if DEBUG
            FarmStore.Reset();
            foreach (var report in await _reportStore.GetReportsAsync())
                await _reportStore.DeleteReportAsync(report.Id);
#endif

            var (parcels, _) = await FarmStore.LoadAsync();

            foreach (var parcel in parcels)
            {
                _parcels.Add(parcel);
                if (parcel.Geometry is null) continue;

                var parcelGraphic = new Graphic(parcel.Geometry, _parcelSymbol);
                _parcelOverlay.Graphics.Add(parcelGraphic);
                _parcelGraphics[parcelGraphic] = parcel;

                var label = new TextSymbol(
                    ParcelLabelText(parcel.Name, parcel.Acres, parcel.Crop), Color.White, 11,
                    Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                    Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle)
                {
                    HaloColor = Color.FromArgb(190, 10, 30, 20),
                    HaloWidth = 2
                };
                if (parcel.Geometry.Extent is { } extent)
                    _parcelOverlay.Graphics.Add(new Graphic(extent.GetCenter(), label));
            }

            ParcelCountLabel.Text = $"{_parcels.Count} total";

            if (_parcels.FirstOrDefault()?.Geometry is { } geometry)
                await mapView.SetViewpointGeometryAsync(geometry, 120);
        }

        await DrawNeighborsAsync();
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
        _incidentGraphics.Clear();

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
                    ReportId = report.Id,
                    FieldName = FieldNameAt(location)
                };

                _incidents.Add(incident);
                DrawIncident(incident);
            }
        }

        RunRiskAnalysis();

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = $"{_incidents.Count} total";
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
                GeometryEngine.Project(p.Geometry, location.SpatialReference ?? SpatialReferences.Wgs84),
                location))?.Name ?? "Unassigned";

    // ---------- neighbouring farms ----------

    private async Task DrawNeighborsAsync()
    {
        _neighborOverlay.Graphics.Clear();
        _neighborGraphics.Clear();

        var farms = await AlertService.GetFarmsAsync();

        var symbol = new SimpleMarkerSymbol(
            SimpleMarkerSymbolStyle.Square,
            Color.FromArgb(215, 96, 165, 250), 11)
        {
            Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.White, 1.5f)
        };

        foreach (var farm in farms)
        {
            if (farm.Location is null) continue;

            var graphic = new Graphic(farm.Location, symbol);
            _neighborOverlay.Graphics.Add(graphic);
            _neighborGraphics[graphic] = farm;

            var label = new TextSymbol(
                $"{farm.FarmName}\n{farm.Crop}",
                Color.FromArgb(255, 191, 219, 254), 9,
                Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                Esri.ArcGISRuntime.Symbology.VerticalAlignment.Top)
            {
                HaloColor = Color.FromArgb(205, 12, 26, 46),
                HaloWidth = 2,
                OffsetY = -14
            };
            _neighborOverlay.Graphics.Add(new Graphic(farm.Location, label));
        }
    }

    private async Task RunAlertsAsync(Incident incident)
    {
        if (incident.Location is null) return;

        if (!incident.AlertNeighbors)
        {
            incident.AlertedFarmCount = 0;
            return;
        }

        var farms = await AlertService.GetFarmsAsync();

        if (farms.Count == 0)
        {
            farms = await AlertService.SeedAroundAsync(incident.Location);
            await DrawNeighborsAsync();
        }

        var recipients = AlertService.ComputeRecipients(
            incident, farms, incident.AlertRadiusMiles);

        incident.AlertedFarmCount = recipients.Count;

        if (recipients.Count == 0)
        {
            await DisplayAlertAsync("No alerts sent",
                $"No farms within {incident.AlertRadiusMiles:F0} miles are growing a crop " +
                $"susceptible to {incident.PestName}.", "OK");
            return;
        }

        double acres = recipients.Sum(r => r.Farm.Acres);

        var lines = string.Join("\n\n", recipients.Select(r =>
            $"- {r.Farm.FarmName} - {r.Farm.Owner}\n" +
            $"   {r.DistanceMiles:F1} mi | {r.Farm.Crop}\n" +
            $"   Matched: {r.Reason}"));

        await DisplayAlertAsync(
            $"Alerted {recipients.Count} farm(s)",
            $"{acres:F0} acres of susceptible crop within " +
            $"{incident.AlertRadiusMiles:F0} miles of this {incident.Severity} " +
            $"{incident.PestName} report.\n\n{lines}",
            "Done");
    }

    // ---------- tools ----------

    private void OnPolygonToolClicked(object sender, EventArgs e)
    {
        CloseActionMenu();
        _geometryEditor.Tool = new VertexTool();
        StartDrawing();
    }

    private void OnFreehandToolClicked(object sender, EventArgs e)
    {
        CloseActionMenu();
        _geometryEditor.Tool = new FreehandTool();
        StartDrawing();
    }

    private void StartDrawing()
    {
        if (!_geometryEditor.IsStarted)
            _geometryEditor.Start(GeometryType.Polygon);

        ConfirmButton.IsEnabled = true;
        ConfirmButton.IsVisible = true;
    }

    private void OnUndoClicked(object sender, EventArgs e)
    {
        CloseActionMenu();
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
        CloseActionMenu();
        if (!_geometryEditor.IsStarted) return;

        var geometry = _geometryEditor.Stop();
        ConfirmButton.IsEnabled = false;
        ConfirmButton.IsVisible = false;
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

        string crop = await DisplayPromptAsync(
            "Crop in this field",
            "Which crop grows here? Used to model pest/blight spread.",
            placeholder: "e.g. Yellow Field Corn (Dent)",
            initialValue: "") ?? "";

        crop = crop.Trim();

        double acres = Math.Abs(GeometryEngine.AreaGeodetic(
            polygon, AreaUnits.Acres, GeodeticCurveType.Geodesic));

        var parcelGraphic = new Graphic(polygon, _parcelSymbol);
        _parcelOverlay.Graphics.Add(parcelGraphic);

        var labelSymbol = new TextSymbol(
            ParcelLabelText(name, acres, crop), Color.White, 11,
            Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
            Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle)
        {
            HaloColor = Color.FromArgb(190, 10, 30, 20),
            HaloWidth = 2
        };
        _parcelOverlay.Graphics.Add(
            new Graphic(polygon.Extent.GetCenter(), labelSymbol));

        var parcel = new Parcel
        {
            Name = name,
            Crop = crop,
            Acres = acres,
            MappedDate = DateTime.Now,
            Geometry = polygon
        };
        _parcelGraphics[parcelGraphic] = parcel;
        _parcels.Insert(0, parcel);

        ParcelCountLabel.Text = $"{_parcels.Count} total";

        // Seed the neighbour registry around the first field the farmer maps.
        if (_parcels.Count == 1 && polygon.Extent?.GetCenter() is { } centre)
        {
            var existing = await AlertService.GetFarmsAsync();
            if (existing.Count == 0)
            {
                await AlertService.SeedAroundAsync(centre);
                await DrawNeighborsAsync();
            }
        }

        // Boundaries changed - recompute exposure.
        if (_incidents.Count > 0) RunRiskAnalysis();
        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    // ---------- zoom to parcel ----------

    private async void OnParcelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Parcel p && p.Geometry is not null)
            await mapView.SetViewpointGeometryAsync(p.Geometry, 60);
    }

    private void OnParcelTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element { BindingContext: Parcel parcel })
            parcel.IsExpanded = !parcel.IsExpanded;
    }

    private async void OnRemoveParcelClicked(object? sender, EventArgs e)
    {
        if (sender is not Element { BindingContext: Parcel parcel }) return;
        bool confirm = await DisplayAlertAsync("Remove parcel", $"Remove \"{parcel.Name}\"? This cannot be undone.", "Remove", "Cancel");
        if (!confirm) return;
        _parcels.Remove(parcel);
        RedrawParcels();
        ParcelCountLabel.Text = RecordsContainer.IsVisible ? $"{_incidents.Count} Total" : $"{_parcels.Count} Total";
        if (_incidents.Count > 0) RunRiskAnalysis();
        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    private void RedrawParcels()
    {
        _parcelOverlay.Graphics.Clear();
        _parcelGraphics.Clear();
        foreach (var parcel in _parcels)
        {
            if (parcel.Geometry is null) continue;
            var parcelGraphic = new Graphic(parcel.Geometry, _parcelSymbol);
            _parcelOverlay.Graphics.Add(parcelGraphic);
            _parcelGraphics[parcelGraphic] = parcel;
            var label = new TextSymbol(ParcelLabelText(parcel.Name, parcel.Acres, parcel.Crop), Color.White, 11, Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center, Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle) { HaloColor = Color.FromArgb(190, 10, 30, 20), HaloWidth = 2 };
            if (parcel.Geometry.Extent is { } extent) _parcelOverlay.Graphics.Add(new Graphic(extent.GetCenter(), label));
        }
    }

    // ---------- incident reporting ----------

    private async void OnReportIncidentClicked(object sender, EventArgs e)
    {
        CloseActionMenu();
        if (_geometryEditor.IsStarted) _geometryEditor.Stop();

        var choice = await DisplayActionSheetAsync(
            "Incident location", "Cancel", null, "Use current location", "Select on map");

        if (choice == "Select on map")
        {
            _awaitingIncidentTap = true;
            return;
        }

        if (choice == "Use current location")
            await ReportFromCurrentLocationAsync();
    }

    private async Task ReportFromCurrentLocationAsync()
    {
        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
                throw new InvalidOperationException("Location permission was not granted.");

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is null)
                throw new InvalidOperationException("The device did not return a location.");

            var point = new MapPoint(location.Longitude, location.Latitude, SpatialReferences.Wgs84);
            await OpenIncidentFormAsync(point, "Current location");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Current location unavailable", ex.Message, "OK");
        }
    }

    private async Task OpenIncidentFormAsync(MapPoint point, string locationSource)
    {
        var page = new IncidentReportPage(point, locationSource);
        await Navigation.PushModalAsync(new NavigationPage(page));
        var incident = await page.Result;
        if (incident is null) return;

        incident.FieldName = FieldNameAt(point);
        _incidents.Add(incident);
        DrawIncident(incident);
        RunRiskAnalysis();

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = $"{_incidents.Count} total";

        await FarmStore.SaveAsync(_parcels, _incidents);
        await RunAlertsAsync(incident);
    }



	// ---------- AI spread modeling ----------

	private async void OnModelSpreadClicked(object sender, EventArgs e)
	{
		CloseActionMenu();

		var located = _incidents.Where(i => i.Location is not null).ToList();
		if (located.Count == 0)
		{
			await DisplayAlertAsync("No reports yet",
				"Report at least one incident before modeling spread.", "OK");
			return;
		}

		// Group by disease TYPE (Classification), not the individual pest/point name.
		var diseaseTypes = located
			.Select(i => string.IsNullOrWhiteSpace(i.Classification) ? i.PestName : i.Classification)
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(t => t)
			.ToList();

		if (diseaseTypes.Count == 0)
		{
			await DisplayAlertAsync("No disease type",
				"Reported incidents have no disease type to model.", "OK");
			return;
		}

		BuildSpreadTypeList(diseaseTypes, located);
		OpenSpreadModal();
	}

	// Populates the modern in-app picker with one selectable card per disease type.
	private void BuildSpreadTypeList(IEnumerable<string> diseaseTypes, List<Incident> located)
	{
		SpreadTypeList.Children.Clear();

		foreach (var type in diseaseTypes)
		{
			string diseaseType = type;
			int count = located.Count(i =>
				string.Equals(i.Classification, diseaseType, StringComparison.OrdinalIgnoreCase) ||
				(string.IsNullOrWhiteSpace(i.Classification) &&
				 string.Equals(i.PestName, diseaseType, StringComparison.OrdinalIgnoreCase)));

			var card = new Border
			{
				BackgroundColor = MauiColor.FromArgb("#FFF7ED"),
				Stroke = MauiColor.FromArgb("#FED7AA"),
				StrokeThickness = 1,
				Padding = new Thickness(14, 12),
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 }
			};

			var grid = new Microsoft.Maui.Controls.Grid { ColumnSpacing = 10 };
			grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
			grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

			var dot = new Border
			{
				BackgroundColor = MauiColor.FromArgb("#C2410C"),
				Stroke = MauiColor.Parse("Transparent"),
				WidthRequest = 34,
				HeightRequest = 34,
				Padding = 0,
				VerticalOptions = LayoutOptions.Center,
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
				Content = new Label
				{
					Text = "🦠",
					FontSize = 16,
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center
				}
			};
			Microsoft.Maui.Controls.Grid.SetColumn(dot, 0);

			var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
			info.Children.Add(new Label
			{
				Text = diseaseType,
				FontSize = 14,
				FontAttributes = FontAttributes.Bold,
				FontFamily = "SpaceGrotesk",
				TextColor = MauiColor.FromArgb("#7C2D12")
			});
			info.Children.Add(new Label
			{
				Text = count == 1 ? "1 report" : $"{count} reports",
				FontSize = 11,
				TextColor = MauiColor.FromArgb("#A97155")
			});
			Microsoft.Maui.Controls.Grid.SetColumn(info, 1);

			var chevron = new Label
			{
				Text = "›",
				FontSize = 22,
				TextColor = MauiColor.FromArgb("#C2410C"),
				VerticalOptions = LayoutOptions.Center
			};
			Microsoft.Maui.Controls.Grid.SetColumn(chevron, 2);

			grid.Children.Add(dot);
			grid.Children.Add(info);
			grid.Children.Add(chevron);
			card.Content = grid;

			card.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(async () => await RunSpreadModelAsync(diseaseType, located))
			});

			SpreadTypeList.Children.Add(card);
		}
	}

	private async Task RunSpreadModelAsync(string diseaseType, List<Incident> located)
	{
		var matching = located.Where(i =>
			string.Equals(i.Classification, diseaseType, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(i.PestName, diseaseType, StringComparison.OrdinalIgnoreCase))
			.Select(i => new Incident
			{
				Classification = i.Classification,
				PestName = i.PestName,
				Severity = i.Severity,
				AffectedCrop = i.AffectedCrop,
				FieldName = i.FieldName,
				ReportDate = i.ReportDate,
				Notes = i.Notes,
				// The spread model works in WGS84 degrees, but incident locations may be
				// stored in Web Mercator. Normalize so the prompt and forecast points
				// share the same coordinate system.
				Location = i.Location is null
					? null
					: GeometryEngine.Project(i.Location, SpatialReferences.Wgs84) as MapPoint
			})
			.ToList();

		SpreadPickerSection.IsVisible = false;
		SpreadResultSection.IsVisible = false;
		SpreadBusyRow.IsVisible = true;
		SpreadBusyLabel.Text = $"Modeling spread of {diseaseType}…";

		try
		{
			var forecast = await PestClassifier.PredictSpreadAsync(diseaseType, matching, _parcels);
			RenderSpreadForecast(forecast);

			System.Diagnostics.Debug.WriteLine(
				$"[Spread] type='{diseaseType}' located={located.Count} matching={matching.Count} points={forecast.Points.Count} graphics={_spreadOverlay.Graphics.Count}");

			SpreadLayerSubtitle.Text = diseaseType;
			SpreadLayerSwitch.IsToggled = true;
			SpreadLayerChip.IsVisible = _spreadOverlay.Graphics.Count > 0;

			if (_spreadOverlay.Graphics.Count == 0)
			{
				SpreadBusyRow.IsVisible = false;
				SpreadPickerSection.IsVisible = true;
				await DisplayAlertAsync(
					"No spread to show",
					$"located={located.Count}, matching={matching.Count}, points={forecast.Points.Count}. " +
					(matching.Count == 0
						? $"No reports are classified as \"{diseaseType}\"."
						: "The model returned no projected points."),
					"OK");
				return;
			}

			SpreadResultTitle.Text = $"Forecast · {diseaseType}";
			SpreadResultNarrative.Text = string.IsNullOrWhiteSpace(forecast.Narrative)
				? "No projection was returned."
				: forecast.Narrative;

			SpreadBusyRow.IsVisible = false;
			SpreadResultSection.IsVisible = true;
		}
		catch (Exception ex)
		{
			SpreadBusyRow.IsVisible = false;
			SpreadPickerSection.IsVisible = true;
			await DisplayAlertAsync("Spread model failed", ex.Message, "OK");
		}
	}

	private void OpenSpreadModal()
	{
		SpreadPickerSection.IsVisible = true;
		SpreadBusyRow.IsVisible = false;
		SpreadResultSection.IsVisible = false;
		SpreadModal.IsVisible = true;
	}

	private void OnCloseSpreadModal(object sender, EventArgs e)
	{
		SpreadModal.IsVisible = false;
	}

	// Toggles visibility of the projected-outbreak overlay on the map.
	private void OnSpreadLayerToggled(object sender, ToggledEventArgs e)
	{
		_spreadOverlay.IsVisible = e.Value;
	}

	// Renders projected outbreak points as a density heatmap. The ArcGIS Maps SDK for
	// .NET has no HeatmapRenderer, so we geodesically buffer and union the projected points
	// into one filled shape per gradient band, producing a heatmap blob spanning the area.
	private void RenderSpreadForecast(SpreadForecast forecast)
	{
		_spreadOverlay.Graphics.Clear();

		// Draw the projection as tiered, organic blobs. Radii are kept small for a
		// conservative estimate; each tier only includes points at/above its intensity
		// threshold, and edges are roughened so shapes read as natural, not circular.
		static Geometry? BuildTier(IEnumerable<SpreadPoint> pts, int minIntensity, double baseRadius, double perWeight)
		{
			var buffers = new List<Geometry>();
			foreach (var point in pts)
			{
				int w = Math.Clamp(point.Intensity, 1, 5);
				if (w < minIntensity) continue;
				var loc = new MapPoint(point.Longitude, point.Latitude, SpatialReferences.Wgs84);
				double radiusMeters = baseRadius + ((w - minIntensity) * perWeight);
				var buffer = GeometryEngine.BufferGeodetic(
					loc, radiusMeters, LinearUnits.Meters, double.NaN, GeodeticCurveType.Geodesic);
				if (buffer is not null) buffers.Add(buffer);
			}
			if (buffers.Count == 0) return null;

			var merged = GeometryEngine.Union(buffers);
			if (merged is null) return null;

			// Roughen the outline: densify then generalize so the edge deviates from a
			// perfect arc, giving a more natural, irregular boundary.
			var dense = GeometryEngine.DensifyGeodetic(merged, 8, LinearUnits.Meters, GeodeticCurveType.Geodesic);
			return GeometryEngine.Generalize(dense ?? merged, 6, false) ?? merged;
		}

		// (outer/watch, mid/affected, core/hot) - tightened radii, increasing opacity.
		var tiers = new (int minIntensity, double baseRadius, double perWeight, int alpha)[]
		{
			(1, 14, 4, 55),
			(3, 9, 4, 105),
			(4, 6, 4, 165)
		};

		foreach (var (minIntensity, baseRadius, perWeight, alpha) in tiers)
		{
			var tier = BuildTier(forecast.Points, minIntensity, baseRadius, perWeight);
			if (tier is null) continue;

			var fill = new SimpleFillSymbol(
				SimpleFillSymbolStyle.Solid,
				Color.FromArgb(alpha, 220, 60, 20),
				new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.FromArgb(Math.Min(alpha + 40, 220), 190, 40, 10), 1));
			_spreadOverlay.Graphics.Add(new Graphic(tier, fill));
		}

		_spreadOverlay.IsVisible = _spreadOverlay.Graphics.Count > 0;

		if (forecast.Points.Count > 0)
		{
			var f = forecast.Points[0];
			System.Diagnostics.Debug.WriteLine($"[SpreadPt] first lat={f.Latitude:F6} lon={f.Longitude:F6}, extent={_spreadOverlay.Extent}");
		}
	}

    private async void OnMapTapped(object? sender,
        Esri.ArcGISRuntime.Maui.GeoViewInputEventArgs e)
    {
        if (!_awaitingIncidentTap || e.Location is null)
        {
            await ShowFeatureDetailsAsync(e.Position);
            return;
        }

        _awaitingIncidentTap = false;

        await OpenIncidentFormAsync(e.Location, "Map selection");
    }

    // Identifies the tapped graphic (incident pins take priority over parcels) and
    // shows an easy-to-read summary of its attributes.
    private async Task ShowFeatureDetailsAsync(Microsoft.Maui.Graphics.Point screenPoint)
    {
        try
        {
            var incidentHit = await mapView.IdentifyGraphicsOverlayAsync(
                _incidentOverlay, screenPoint, 12, false, 1);

            if (incidentHit.Graphics.FirstOrDefault() is { } incidentGraphic &&
                _incidentGraphics.TryGetValue(incidentGraphic, out var incident))
            {
                await ShowIncidentDetailsAsync(incident);
                return;
            }

            var neighborHit = await mapView.IdentifyGraphicsOverlayAsync(
                _neighborOverlay, screenPoint, 12, false, 1);

            if (neighborHit.Graphics.FirstOrDefault() is { } neighborGraphic &&
                _neighborGraphics.TryGetValue(neighborGraphic, out var farm))
            {
                await ShowNeighborDetailsAsync(farm);
                return;
            }

            var parcelHit = await mapView.IdentifyGraphicsOverlayAsync(
                _parcelOverlay, screenPoint, 12, false, 1);

            if (parcelHit.Graphics.FirstOrDefault() is { } parcelGraphic &&
                _parcelGraphics.TryGetValue(parcelGraphic, out var parcel))
            {
                await ShowParcelDetailsAsync(parcel);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Unable to inspect", ex.Message, "OK");
        }
    }

    private async Task ShowIncidentDetailsAsync(Incident incident)
    {
        var details =
            $"Severity:  {incident.Severity}\n" +
            $"Status:    {incident.Status}\n" +
            $"Category:  {incident.Classification}\n" +
            $"Field:     {incident.FieldName}\n" +
            $"Reported:  {incident.DateDisplay}\n" +
            $"Sharing:   {incident.AlertDisplay}";

        if (!string.IsNullOrWhiteSpace(incident.Notes))
            details += $"\n\nNotes:\n{incident.Notes}";

        if (!string.IsNullOrWhiteSpace(incident.Treatment))
            details += $"\n\nRecommended action:\n{incident.Treatment}";

        var title = string.IsNullOrWhiteSpace(incident.PestName)
            ? "Report" : incident.PestName;

        await DisplayAlertAsync(title, details, "Close");
    }

    private async Task ShowNeighborDetailsAsync(NeighborFarm farm)
    {
        var details =
            $"Owner:  {farm.Owner}\n" +
            $"Crop:   {farm.Crop}\n" +
            $"Area:   {farm.Acres:F1} Ac\n\n" +
            "Registered on FarmGuard - receives alerts for nearby outbreaks affecting this crop.";

        await DisplayAlertAsync(farm.FarmName, details, "Close");
    }

    // Builds the map label for a parcel: name + acreage, with the crop type on a second line.
    private static string ParcelLabelText(string name, double acres, string crop) =>
        string.IsNullOrWhiteSpace(crop)
            ? $"{name}  {acres:F1} Ac"
            : $"{name}  {acres:F1} Ac\n{crop}";

    private async Task ShowParcelDetailsAsync(Parcel parcel)
    {
        int reportsHere = _incidents.Count(i =>
            string.Equals(i.FieldName, parcel.Name, StringComparison.OrdinalIgnoreCase));

        var details =
            $"Area:      {parcel.AcresDisplay}\n" +
            $"Crop:      {(string.IsNullOrWhiteSpace(parcel.Crop) ? "\u2014" : parcel.Crop)}\n" +
            $"{parcel.MappedDisplay}\n" +
            $"Reports:   {reportsHere}";

        await DisplayAlertAsync(parcel.Name, details, "Close");
    }

    private void DrawIncident(Incident inc)
    {
        if (inc.Location is null) return;

        var color = inc.Severity switch
        {
            "CRITICAL" => Color.FromArgb(255, 220, 38, 38),
            "HIGH" => Color.FromArgb(255, 234, 88, 12),
            "MEDIUM" => Color.FromArgb(255, 202, 138, 4),
            _ => Color.FromArgb(255, 220, 38, 38)
        };

        var symbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, color, 16)
        {
            Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.White, 2)
        };

        var graphic = new Graphic(inc.Location, symbol);
        _incidentOverlay.Graphics.Add(graphic);
        _incidentGraphics[graphic] = inc;
    }

    // ---------- spatial risk propagation ----------

    private void RunRiskAnalysis()
    {
        _threatOverlay.Graphics.Clear();
        _riskOverlay.Graphics.Clear();

        var result = RiskEngine.Analyze(_incidents, _parcels);


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
            $"{result.AtRisk.Count} neighboring field(s) within spread radius - " +
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
        _incidentGraphics.Clear();
        _threatOverlay.Graphics.Clear();
        _riskOverlay.Graphics.Clear();
        RiskList.ItemsSource = null;
        ThreatPanel.IsVisible = false;

        if (RecordsContainer.IsVisible)
            ParcelCountLabel.Text = "0 total";

        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    // ---------- panel tabs ----------

    private void OnShowParcels(object sender, EventArgs e)
    {
        ParcelList.IsVisible = true;
        RecordsContainer.IsVisible = false;
        PanelTitleLabel.Text = "Mapped parcels";
        ParcelCountLabel.Text = $"{_parcels.Count} total";
        AddBoundaryButton.IsVisible = true;

        ParcelsTabButton.BackgroundColor = MauiColor.FromArgb("#2E7D32");
        ParcelsTabButton.TextColor = Colors.White;
        RecordsTabButton.BackgroundColor = MauiColor.FromArgb("#DDE8F0");
        RecordsTabButton.TextColor = MauiColor.FromArgb("#5A6E77");
    }

    private void OnShowRecords(object sender, EventArgs e)
    {
        ParcelList.IsVisible = false;
        RecordsContainer.IsVisible = true;
        PanelTitleLabel.Text = "Records & log";
        ParcelCountLabel.Text = $"{_incidents.Count} total";
        AddBoundaryButton.IsVisible = false;

        RecordsTabButton.BackgroundColor = MauiColor.FromArgb("#2E7D32");
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
            await DisplayAlertAsync($"{inc.PestName} - {inc.Severity}",
                $"{inc.Notes}\n\nRecommended action:\n{inc.Treatment}", "Close");
    }

    private void OnIncidentTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element { BindingContext: Incident incident })
            incident.IsExpanded = !incident.IsExpanded;
    }

    private async void OnRemoveIncidentClicked(object? sender, EventArgs e)
    {
        if (sender is not Element { BindingContext: Incident incident }) return;
        bool confirm = await DisplayAlertAsync("Remove record", $"Remove the \"{incident.PestName}\" report? This cannot be undone.", "Remove", "Cancel");
        if (!confirm) return;
        if (incident.ReportId != Guid.Empty) await _reportStore.DeleteReportAsync(incident.ReportId);
        _incidents.Remove(incident);
        var graphic = _incidentGraphics.FirstOrDefault(kvp => kvp.Value == incident).Key;
        if (graphic is not null) { _incidentOverlay.Graphics.Remove(graphic); _incidentGraphics.Remove(graphic); }
        RunRiskAnalysis();
        if (RecordsContainer.IsVisible) ParcelCountLabel.Text = $"{_incidents.Count} Total";
        await FarmStore.SaveAsync(_parcels, _incidents);
    }

    private async void OnGenerateSummary(object sender, EventArgs e)
    {
        if (_incidents.Count == 0)
        {
            AiSummaryLabel.Text = "No incidents logged yet.";
            return;
        }

        SummaryButton.IsEnabled = false;
        AiSummaryLabel.Text = "Analyzing incident history...";

        AiSummaryLabel.Text = await PestClassifier.SummarizeAsync(_incidents);

        SummaryButton.IsEnabled = true;
    }

    // ---------- temporary AI test (delete before the demo) ----------

    private async void OnTestAiClicked(object sender, EventArgs e)
    {
        CloseActionMenu();
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
            result is null ? "Failed - check Output window"
                           : $"{result.PestName}\n{result.Severity} - {result.Confidence}%\n\n{result.Notes}",
            "OK");
    }
}
