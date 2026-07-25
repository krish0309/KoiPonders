using Esri.ArcGISRuntime.Geometry;

namespace KoiPonders;

public partial class IncidentReportPage : ContentPage
{
    private readonly TaskCompletionSource<Incident?> _tcs = new();
    private readonly MapPoint _location;
    private byte[]? _photo;
    private Incident? _draft;

    public Task<Incident?> Result => _tcs.Task;

    public IncidentReportPage(MapPoint location)
    {
        InitializeComponent();
        _location = location;

        var wgs = location.SpatialReference is { Wkid: 4326 }
            ? location
            : GeometryEngine.Project(location, SpatialReferences.Wgs84) as MapPoint;

        LocationLabel.Text = wgs is null ? "" : $"Lat {wgs.Y:F5}, Lon {wgs.X:F5}";

        CategoryPicker.ItemsSource = new List<string>
        {
            "Pest Infestation", "Crop Blight", "Fungal Disease",
            "Bacterial Infection", "Healthy"
        };
        SeverityPicker.ItemsSource = new List<string>
        {
            "LOW", "MEDIUM", "HIGH", "CRITICAL"
        };
    }

    // ---------- step 1 ----------

    private async void OnTakePhoto(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlertAsync("Camera unavailable",
                    "No camera on this device — use Upload Photo instead.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is not null) await LoadPhoto(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Camera error", ex.Message, "OK");
        }
    }

    private async void OnUploadPhoto(object sender, EventArgs e)
    {
        var photo = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "Photo evidence"
        });
        if (photo is not null) await LoadPhoto(photo);
    }

    private async Task LoadPhoto(FileResult file)
    {
        using var stream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        _photo = ms.ToArray();

        PhotoPreview.Source = ImageSource.FromStream(() => new MemoryStream(_photo));
        PhotoFrame.IsVisible = true;
        Step1StatusLabel.Text = "Photo attached.";
    }

    private async void OnAnalyze(object sender, EventArgs e)
    {
        string text = DescriptionEditor.Text?.Trim() ?? "";

        if (_photo is null && text.Length < 5)
        {
            await DisplayAlertAsync("Nothing to analyze",
                "Add a photo or describe what you're seeing.", "OK");
            return;
        }

        AnalyzeButton.IsEnabled = false;
        Step1StatusLabel.Text = "Analyzing…";

        _draft = _photo is not null
            ? await PestClassifier.ClassifyAsync(_photo)
            : await PestClassifier.ClassifyTextAsync(text);

        AnalyzeButton.IsEnabled = true;

        if (_draft is null)
        {
            Step1StatusLabel.Text = "Analysis failed — fill the form in manually.";
            _draft = new Incident { Severity = "MEDIUM", Confidence = 0 };
        }
        else
        {
            Step1StatusLabel.Text = "";
        }

        if (_photo is null && text.Length > 0 && string.IsNullOrWhiteSpace(_draft.Notes))
            _draft.Notes = text;

        PopulateStep2();
        Step1.IsVisible = false;
        Step2.IsVisible = true;
        TitleLabel.Text = "Confirm the Diagnosis";
    }

    private void PopulateStep2()
    {
        if (_draft is null) return;

        AiBadgeLabel.Text = _draft.Confidence > 0
            ? $"AI suggestion · {_draft.Confidence}% confidence"
            : "No AI result — fill this in yourself";

        ProblemEntry.Text = _draft.PestName;
        CropEntry.Text = _draft.AffectedCrop;
        ObservedEditor.Text = _draft.Notes;
        TreatmentEditor.Text = _draft.Treatment;

        CategoryPicker.SelectedItem =
            (CategoryPicker.ItemsSource as List<string>)?
            .FirstOrDefault(c => c == _draft.Classification) ?? "Pest Infestation";

        SeverityPicker.SelectedItem =
            (SeverityPicker.ItemsSource as List<string>)?
            .FirstOrDefault(s => s == _draft.Severity) ?? "MEDIUM";
    }

    // ---------- step 2 ----------

    private void OnBack(object sender, EventArgs e)
    {
        Step2.IsVisible = false;
        Step1.IsVisible = true;
        TitleLabel.Text = "Report an Incident";
    }

    private async void OnSave(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProblemEntry.Text))
        {
            await DisplayAlertAsync("Missing problem", "Give the problem a name.", "OK");
            return;
        }

        string userNotes = UserNotesEditor.Text?.Trim() ?? "";
        string observed = ObservedEditor.Text?.Trim() ?? "";

        var incident = new Incident
        {
            PestName = ProblemEntry.Text.Trim(),
            Classification = CategoryPicker.SelectedItem as string ?? "Pest Infestation",
            Severity = SeverityPicker.SelectedItem as string ?? "MEDIUM",
            AffectedCrop = CropEntry.Text?.Trim() ?? "",
            Notes = string.IsNullOrEmpty(userNotes) ? observed : $"{observed}\n\nFarmer: {userNotes}",
            Treatment = TreatmentEditor.Text?.Trim() ?? "",
            Confidence = _draft?.Confidence ?? 0,
            Location = _location,
            Photo = _photo,
            ReportDate = DateTime.Now,
            Status = "OPEN"
        };

        _tcs.TrySetResult(incident);
        await Navigation.PopModalAsync();
    }

    private async void OnCancel(object sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}