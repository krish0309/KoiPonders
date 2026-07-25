using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Mvvm;
using KoiPonders.Services;

namespace KoiPonders.ViewModels
{
	public sealed class StatCard
	{
		public string Value { get; set; } = string.Empty;

		public string Label { get; set; } = string.Empty;

		public string Accent { get; set; } = "#2E9E56";
	}

	public sealed class ShortcutItem
	{
		public string Icon { get; set; } = string.Empty;

		public string Title { get; set; } = string.Empty;

		public string Subtitle { get; set; } = string.Empty;

		public string Route { get; set; } = string.Empty;
	}

	/// <summary>
	/// Backs the "Oakridge Farm Central" dashboard: live farm stats plus platform shortcuts.
	/// </summary>
	public sealed class DashboardViewModel : ObservableObject
	{
		private readonly IFarmDataStore _dataStore;
		private string _weatherSummary = "72°  Sunny";
		private string _welcomeSubtitle = "All zones nominal. Summer maintenance window closes soon.";

		public DashboardViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

			NavigateCommand = new RelayCommand(async parameter =>
			{
				if (parameter is string route && !string.IsNullOrWhiteSpace(route))
				{
					await Shell.Current.GoToAsync($"//{route}");
				}
			});

			Shortcuts.Add(new ShortcutItem { Icon = "▤", Title = "3D Boundary Mapper", Subtitle = "Draw & edit field parcels", Route = "MainPage" });
			Shortcuts.Add(new ShortcutItem { Icon = "⚠", Title = "Report Pest/Blight", Subtitle = "Log a new field incident", Route = "LogIncidentPage" });
			Shortcuts.Add(new ShortcutItem { Icon = "◎", Title = "GeoAI Predictive Analysis", Subtitle = "Run epidemiology forecast", Route = "GeoAiPage" });
			Shortcuts.Add(new ShortcutItem { Icon = "▦", Title = "Spatial Records", Subtitle = "Browse the database log", Route = "RecordsPage" });
		}

		public ObservableCollection<StatCard> Stats { get; } = new();

		public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

		public ICommand NavigateCommand { get; }

		public string WeatherSummary
		{
			get => _weatherSummary;
			set => SetProperty(ref _weatherSummary, value);
		}

		public string WelcomeSubtitle
		{
			get => _welcomeSubtitle;
			set => SetProperty(ref _welcomeSubtitle, value);
		}

		public async Task LoadAsync()
		{
			var fields = await _dataStore.GetFieldsAsync();
			var reports = await _dataStore.GetAllReportsAsync();
			var totalAcres = fields.Sum(f => f.AreaHectares * 2.47105);
			var openReports = reports.Count(r => r.Report.Status != Models.ReportStatus.Resolved);
			var zones = fields.Select(f => f.Name).Distinct().Count();

			Stats.Clear();
			Stats.Add(new StatCard { Value = $"{totalAcres:0.0} Ac", Label = "Mapped Acreage", Accent = "#2E9E56" });
			Stats.Add(new StatCard { Value = reports.Count.ToString(), Label = "Reported", Accent = "#E1A100" });
			Stats.Add(new StatCard { Value = zones.ToString(), Label = "Zones", Accent = "#2E7DD1" });
			Stats.Add(new StatCard { Value = openReports.ToString(), Label = "Active", Accent = "#B71C1C" });
		}
	}
}
