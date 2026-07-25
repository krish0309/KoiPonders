using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Models;
using KoiPonders.Mvvm;
using KoiPonders.Services;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Backs the "Log Field Incident" anomaly form (severity, category, evidence, drop location).
	/// </summary>
	public sealed class LogIncidentViewModel : ObservableObject
	{
		private readonly IFarmDataStore _dataStore;
		private string _title = string.Empty;
		private string _notes = string.Empty;
		private ProblemCategory _category = ProblemCategory.Fungal;
		private Severity _severity = Severity.Moderate;
		private string _dropLocation = "No location set — tap the map to drop a pin.";
		private string _statusMessage = string.Empty;
		private PlantField? _selectedField;

		public LogIncidentViewModel(IFarmDataStore dataStore)
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

			SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !string.IsNullOrWhiteSpace(Title));
		}

		public ObservableCollection<ProblemCategory> Categories { get; } = new();

		public ObservableCollection<Severity> Severities { get; } = new();

		public ObservableCollection<PlantField> Fields { get; } = new();

		public ICommand SubmitCommand { get; }

		public string Title
		{
			get => _title;
			set
			{
				if (SetProperty(ref _title, value) && SubmitCommand is AsyncRelayCommand cmd)
				{
					cmd.RaiseCanExecuteChanged();
				}
			}
		}

		public string Notes
		{
			get => _notes;
			set => SetProperty(ref _notes, value);
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

		public PlantField? SelectedField
		{
			get => _selectedField;
			set => SetProperty(ref _selectedField, value);
		}

		public string DropLocation
		{
			get => _dropLocation;
			set => SetProperty(ref _dropLocation, value);
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

			SelectedField = Fields.FirstOrDefault();
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
				ProblemName = Title,
				Category = Category,
				Severity = Severity,
				Notes = Notes,
				Status = ReportStatus.Open,
			};

			await _dataStore.UpsertReportAsync(SelectedField.Id, report);
			StatusMessage = "Incident logged and geo-tagged to ArcGIS.";
			Title = string.Empty;
			Notes = string.Empty;
		}
	}
}
