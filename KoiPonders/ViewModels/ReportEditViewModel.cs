using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Models;
using KoiPonders.Mvvm;
using KoiPonders.Services;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Creates or edits a <see cref="PestDiseaseReport"/> including photos, treatment
	/// actions, severity, and follow-up status.
	/// </summary>
	public sealed class ReportEditViewModel : ObservableObject, IQueryAttributable
	{
		private readonly IFarmDataStore _dataStore;

		private Guid _fieldId;
		private PestDiseaseReport _report = new();
		private bool _isNew = true;
		private string _fieldName = string.Empty;
		private string _title = "New Report";

		public ReportEditViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

			AddPhotoCommand = new AsyncRelayCommand(AddPhotoAsync);
			RemovePhotoCommand = new AsyncRelayCommand(RemovePhotoAsync);
			AddTreatmentCommand = new AsyncRelayCommand(AddTreatmentAsync);
			RemoveTreatmentCommand = new RelayCommand(RemoveTreatment);
			SaveCommand = new AsyncRelayCommand(SaveAsync);
			DeleteCommand = new AsyncRelayCommand(DeleteAsync);
		}

		public Array Categories { get; } = Enum.GetValues(typeof(ProblemCategory));

		public Array Severities { get; } = Enum.GetValues(typeof(Severity));

		public Array Statuses { get; } = Enum.GetValues(typeof(ReportStatus));

		public ObservableCollection<ReportPhoto> Photos { get; } = new();

		public ObservableCollection<TreatmentAction> Treatments { get; } = new();

		public string Title
		{
			get => _title;
			private set => SetProperty(ref _title, value);
		}

		public string FieldName
		{
			get => _fieldName;
			private set => SetProperty(ref _fieldName, value);
		}

		public bool IsNew
		{
			get => _isNew;
			private set
			{
				if (SetProperty(ref _isNew, value))
				{
					OnPropertyChanged(nameof(CanDelete));
				}
			}
		}

		public bool CanDelete => !IsNew;

		public string ProblemName
		{
			get => _report.ProblemName;
			set { _report.ProblemName = value; OnPropertyChanged(); }
		}

		public ProblemCategory Category
		{
			get => _report.Category;
			set { _report.Category = value; OnPropertyChanged(); }
		}

		public Severity Severity
		{
			get => _report.Severity;
			set { _report.Severity = value; OnPropertyChanged(); }
		}

		public ReportStatus Status
		{
			get => _report.Status;
			set { _report.Status = value; OnPropertyChanged(); }
		}

		public double AffectedAreaPercent
		{
			get => _report.AffectedAreaPercent;
			set { _report.AffectedAreaPercent = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
		}

		public string Notes
		{
			get => _report.Notes;
			set { _report.Notes = value; OnPropertyChanged(); }
		}

		public DateTime ObservedDate
		{
			get => _report.ObservedUtc.LocalDateTime.Date;
			set
			{
				_report.ObservedUtc = new DateTimeOffset(value);
				OnPropertyChanged();
			}
		}

		public DateTime FollowUpDate
		{
			get => (_report.FollowUpUtc ?? DateTimeOffset.Now.AddDays(7)).LocalDateTime.Date;
			set
			{
				_report.FollowUpUtc = new DateTimeOffset(value);
				OnPropertyChanged();
			}
		}

		public ICommand AddPhotoCommand { get; }

		public ICommand RemovePhotoCommand { get; }

		public ICommand AddTreatmentCommand { get; }

		public ICommand RemoveTreatmentCommand { get; }

		public ICommand SaveCommand { get; }

		public ICommand DeleteCommand { get; }

		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			if (query.TryGetValue("fieldId", out var fieldIdValue) &&
				Guid.TryParse(Convert.ToString(fieldIdValue), out var fieldId))
			{
				_fieldId = fieldId;
			}

			Guid? reportId = null;
			if (query.TryGetValue("reportId", out var reportIdValue) &&
				Guid.TryParse(Convert.ToString(reportIdValue), out var parsedReportId))
			{
				reportId = parsedReportId;
			}

			_ = InitializeAsync(reportId);
		}

		private async Task InitializeAsync(Guid? reportId)
		{
			var field = await _dataStore.GetFieldAsync(_fieldId);
			FieldName = field?.Name ?? "Unknown field";

			var existing = reportId.HasValue
				? field?.Reports.FirstOrDefault(r => r.Id == reportId.Value)
				: null;

			if (existing is not null)
			{
				_report = existing;
				IsNew = false;
				Title = "Edit Report";
			}
			else
			{
				_report = new PestDiseaseReport();
				IsNew = true;
				Title = "New Report";
			}

			Photos.Clear();
			foreach (var photo in _report.Photos)
			{
				Photos.Add(photo);
			}

			Treatments.Clear();
			foreach (var treatment in _report.Treatments)
			{
				Treatments.Add(treatment);
			}

			RaiseAllChanged();
		}

		private async Task AddPhotoAsync()
		{
			try
			{
				FileResult? photo = null;
				if (MediaPicker.Default.IsCaptureSupported)
				{
					var action = await Shell.Current.CurrentPage.DisplayActionSheet(
						"Add photo", "Cancel", null, "Take photo", "Choose from library");
					photo = action switch
					{
						"Take photo" => await MediaPicker.Default.CapturePhotoAsync(),
						"Choose from library" => await MediaPicker.Default.PickPhotoAsync(),
						_ => null
					};
				}
				else
				{
					photo = await MediaPicker.Default.PickPhotoAsync();
				}

				if (photo is null)
				{
					return;
				}

				var stored = await CopyPhotoToAppDataAsync(photo);
				Photos.Add(stored);
			}
			catch (FeatureNotSupportedException)
			{
				await Shell.Current.CurrentPage.DisplayAlert(
					"Not supported", "Photo capture is not supported on this device.", "OK");
			}
		}

		private async Task RemovePhotoAsync(object? parameter)
		{
			if (parameter is not ReportPhoto photo)
			{
				return;
			}

			Photos.Remove(photo);
			try
			{
				if (File.Exists(photo.FilePath))
				{
					File.Delete(photo.FilePath);
				}
			}
			catch (IOException)
			{
				// Ignore file deletion failures; the reference is already removed.
			}

			await Task.CompletedTask;
		}

		private async Task AddTreatmentAsync()
		{
			var action = await Shell.Current.CurrentPage.DisplayPromptAsync(
				"Treatment action",
				"What action did you take?",
				"Add",
				"Cancel",
				"e.g. Applied neem oil");

			if (string.IsNullOrWhiteSpace(action))
			{
				return;
			}

			var product = await Shell.Current.CurrentPage.DisplayPromptAsync(
				"Product / method",
				"Product or method used (optional):",
				"Save",
				"Skip");

			Treatments.Add(new TreatmentAction
			{
				Action = action.Trim(),
				Product = product?.Trim() ?? string.Empty
			});
		}

		private void RemoveTreatment(object? parameter)
		{
			if (parameter is TreatmentAction treatment)
			{
				Treatments.Remove(treatment);
			}
		}

		private async Task SaveAsync()
		{
			if (string.IsNullOrWhiteSpace(ProblemName))
			{
				await Shell.Current.CurrentPage.DisplayAlert(
					"Missing info", "Please enter the problem name.", "OK");
				return;
			}

			_report.Photos = Photos.ToList();
			_report.Treatments = Treatments.ToList();

			await _dataStore.UpsertReportAsync(_fieldId, _report);
			await Shell.Current.GoToAsync("..");
		}

		private async Task DeleteAsync()
		{
			var confirm = await Shell.Current.CurrentPage.DisplayAlert(
				"Delete report", "Delete this report permanently?", "Delete", "Cancel");
			if (!confirm)
			{
				return;
			}

			await _dataStore.DeleteReportAsync(_fieldId, _report.Id);
			await Shell.Current.GoToAsync("..");
		}

		private static async Task<ReportPhoto> CopyPhotoToAppDataAsync(FileResult photo)
		{
			var photoDirectory = Path.Combine(FileSystem.AppDataDirectory, "photos");
			Directory.CreateDirectory(photoDirectory);

			var extension = Path.GetExtension(photo.FileName);
			if (string.IsNullOrEmpty(extension))
			{
				extension = ".jpg";
			}

			var targetPath = Path.Combine(photoDirectory, $"{Guid.NewGuid()}{extension}");

			await using var sourceStream = await photo.OpenReadAsync();
			await using var targetStream = File.Create(targetPath);
			await sourceStream.CopyToAsync(targetStream);

			return new ReportPhoto { FilePath = targetPath };
		}

		private void RaiseAllChanged()
		{
			OnPropertyChanged(nameof(ProblemName));
			OnPropertyChanged(nameof(Category));
			OnPropertyChanged(nameof(Severity));
			OnPropertyChanged(nameof(Status));
			OnPropertyChanged(nameof(AffectedAreaPercent));
			OnPropertyChanged(nameof(Notes));
			OnPropertyChanged(nameof(ObservedDate));
			OnPropertyChanged(nameof(FollowUpDate));
		}
	}
}
