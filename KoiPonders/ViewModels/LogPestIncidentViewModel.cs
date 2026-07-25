using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Models;
using KoiPonders.Mvvm;
using KoiPonders.Services;
using KoiPonders.Views;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Backs the detailed dark-themed "Log Pest Incident" form with affected area, dates, and status.
	/// </summary>
	public sealed class LogPestIncidentViewModel : ObservableObject
	{
		private readonly IFarmDataStore _dataStore;
		private string _problemName = string.Empty;
		private ProblemCategory _category = ProblemCategory.Insect;
		private Severity _severity = Severity.Moderate;
		private ReportStatus _status = ReportStatus.Open;
		private double _affectedAreaPercent = 15;
		private DateTime _observedDate = DateTime.Now;
		private DateTime _followUpDate = DateTime.Now.AddDays(7);
		private string _notes = string.Empty;
		private string _statusMessage = string.Empty;
		private PlantField? _selectedField;
		private double? _latitude;
		private double? _longitude;
		private Guid? _pendingFieldId;

		public LogPestIncidentViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

			foreach (var value in Enum.GetValues<ProblemCategory>())
			{
				Categories.Add(value);
			}

			foreach (var value in Enum.GetValues<Severity>())
			{
				Severities.Add(value);
			}

			foreach (var value in Enum.GetValues<ReportStatus>())
			{
				Statuses.Add(value);
			}

			SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !string.IsNullOrWhiteSpace(ProblemName));
			PickLocationCommand = new AsyncRelayCommand(PickLocationAsync);
		}

		public ObservableCollection<ProblemCategory> Categories { get; } = new();

		public ObservableCollection<Severity> Severities { get; } = new();

		public ObservableCollection<ReportStatus> Statuses { get; } = new();

		public ObservableCollection<PlantField> Fields { get; } = new();

		public ICommand SubmitCommand { get; }

		public ICommand PickLocationCommand { get; }

		public double? Latitude
		{
			get => _latitude;
			private set
			{
				if (SetProperty(ref _latitude, value))
				{
					OnPropertyChanged(nameof(HasLocation));
					OnPropertyChanged(nameof(LocationText));
				}
			}
		}

		public double? Longitude
		{
			get => _longitude;
			private set
			{
				if (SetProperty(ref _longitude, value))
				{
					OnPropertyChanged(nameof(HasLocation));
					OnPropertyChanged(nameof(LocationText));
				}
			}
		}

		public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

		public string LocationText => HasLocation
			? $"📍 Lat {Latitude:0.00000}, Lon {Longitude:0.00000}"
			: "No location set — tap to drop a pin on the map.";

		public string ProblemName
		{
			get => _problemName;
			set
			{
				if (SetProperty(ref _problemName, value) && SubmitCommand is AsyncRelayCommand cmd)
				{
					cmd.RaiseCanExecuteChanged();
				}
			}
		}

		public ProblemCategory Category
		{
			get => _category;
			set => SetProperty(ref _category, value);
		}

		public Severity Severity
		{
			get => _severity;
			set => SetProperty(ref _severity, value);
		}

		public ReportStatus Status
		{
			get => _status;
			set => SetProperty(ref _status, value);
		}

		public double AffectedAreaPercent
		{
			get => _affectedAreaPercent;
			set => SetProperty(ref _affectedAreaPercent, value);
		}

		public DateTime ObservedDate
		{
			get => _observedDate;
			set => SetProperty(ref _observedDate, value);
		}

		public DateTime FollowUpDate
		{
			get => _followUpDate;
			set => SetProperty(ref _followUpDate, value);
		}

		public string Notes
		{
			get => _notes;
			set => SetProperty(ref _notes, value);
		}

		public PlantField? SelectedField
		{
			get => _selectedField;
			set => SetProperty(ref _selectedField, value);
		}

		public string StatusMessage
		{
			get => _statusMessage;
			set => SetProperty(ref _statusMessage, value);
		}

		public async Task LoadAsync()
		{
			var fields = await _dataStore.GetFieldsAsync();
			Fields.Clear();
			foreach (var field in fields)
			{
				Fields.Add(field);
			}

			SelectedField = (_pendingFieldId.HasValue
				? Fields.FirstOrDefault(f => f.Id == _pendingFieldId.Value)
				: null) ?? Fields.FirstOrDefault();

			if (_pendingFieldId.HasValue && SelectedField is not null)
			{
				StatusMessage = $"Incident attached to '{SelectedField.Name}'.";
			}
		}

		/// <summary>
		/// Preselects the field this report belongs to (set from a map tap inside a polygon).
		/// </summary>
		public void SetTargetField(Guid fieldId)
		{
			_pendingFieldId = fieldId;
			var match = Fields.FirstOrDefault(f => f.Id == fieldId);
			if (match is not null)
			{
				SelectedField = match;
			}
		}

		/// <summary>
		/// Applies a location returned from the map picker page.
		/// </summary>
		public void ApplyPickedLocation(double latitude, double longitude)
		{
			Latitude = latitude;
			Longitude = longitude;
			StatusMessage = "Report location captured from the map.";
		}

		private async Task PickLocationAsync()
		{
			await Shell.Current.GoToAsync(nameof(Views.ReportLocationPage));
		}

		private async Task SubmitAsync()
		{
			if (SelectedField is null)
			{
				StatusMessage = "Select a field to attach this incident to.";
				return;
			}

			var report = new PestDiseaseReport
			{
				ProblemName = ProblemName,
				Category = Category,
				Severity = Severity,
				Status = Status,
				AffectedAreaPercent = AffectedAreaPercent,
				ObservedUtc = ObservedDate,
				FollowUpUtc = FollowUpDate,
				Notes = Notes,
				Latitude = Latitude,
				Longitude = Longitude,
			};

			await _dataStore.UpsertReportAsync(SelectedField.Id, report);
			StatusMessage = "Pest incident saved to the ArcGIS spatial log.";
			ProblemName = string.Empty;
			Notes = string.Empty;
		}
	}
}
