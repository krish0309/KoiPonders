using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Mvvm;
using KoiPonders.Services;
using KoiPonders.Views;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Lists every pest/disease report across all fields, newest first.
	/// </summary>
	public sealed class ReportsViewModel : ObservableObject
	{
		private readonly IFarmDataStore _dataStore;
		private bool _isRefreshing;

		public ReportsViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

			RefreshCommand = new AsyncRelayCommand(LoadAsync);
			OpenReportCommand = new AsyncRelayCommand(OpenReportAsync);
			NewReportCommand = new AsyncRelayCommand(NewReportAsync);
		}

		public ObservableCollection<ReportListItem> Reports { get; } = new();

		public bool IsRefreshing
		{
			get => _isRefreshing;
			set => SetProperty(ref _isRefreshing, value);
		}

		public bool HasFields { get; private set; }

		public ICommand RefreshCommand { get; }

		public ICommand OpenReportCommand { get; }

		public ICommand NewReportCommand { get; }

		public async Task LoadAsync()
		{
			IsRefreshing = true;
			try
			{
				var reports = await _dataStore.GetAllReportsAsync();
				Reports.Clear();
				foreach (var (field, report) in reports)
				{
					Reports.Add(new ReportListItem(field, report));
				}

				var fields = await _dataStore.GetFieldsAsync();
				HasFields = fields.Count > 0;
				OnPropertyChanged(nameof(HasFields));
			}
			finally
			{
				IsRefreshing = false;
			}
		}

		private async Task OpenReportAsync(object? parameter)
		{
			if (parameter is not ReportListItem item)
			{
				return;
			}

			await Shell.Current.GoToAsync(
				$"{nameof(ReportEditPage)}?fieldId={item.Field.Id}&reportId={item.Report.Id}");
		}

		private async Task NewReportAsync()
		{
			var fields = await _dataStore.GetFieldsAsync();
			if (fields.Count == 0)
			{
				await Shell.Current.CurrentPage.DisplayAlert(
					"No fields",
					"Create a field on the Farm Map before filing a report.",
					"OK");
				return;
			}

			var names = fields.Select(f => f.Name).ToArray();
			var chosen = await Shell.Current.CurrentPage.DisplayActionSheet(
				"Report a problem for which field?", "Cancel", null, names);

			var field = fields.FirstOrDefault(f => f.Name == chosen);
			if (field is null)
			{
				return;
			}

			await Shell.Current.GoToAsync($"{nameof(ReportEditPage)}?fieldId={field.Id}");
		}
	}
}
