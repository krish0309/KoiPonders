using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Mvvm;

namespace KoiPonders.Controls
{
	/// <summary>
	/// Shared FarmGuard navigation rail used by every page so the chrome is identical.
	/// Set <see cref="ActiveRoute"/> to highlight the current page.
	/// </summary>
	public partial class FarmSidebar : ContentView
	{
		public static readonly BindableProperty ActiveRouteProperty = BindableProperty.Create(
			nameof(ActiveRoute), typeof(string), typeof(FarmSidebar), default(string),
			propertyChanged: OnActiveRouteChanged);

		public FarmSidebar()
		{
			InitializeComponent();
			NavigateCommand = new RelayCommand(async parameter =>
			{
				if (parameter is not string route || string.IsNullOrWhiteSpace(route))
				{
					return;
				}

				if (route == ActiveRoute)
				{
					return;
				}

				await Shell.Current.GoToAsync($"//{route}");
			});

			BuildItems();
		}

		public string? ActiveRoute
		{
			get => (string?)GetValue(ActiveRouteProperty);
			set => SetValue(ActiveRouteProperty, value);
		}

		public ICommand NavigateCommand { get; }

		public ObservableCollection<SidebarItem> Items { get; } = new();

		private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
		{
			if (bindable is FarmSidebar sidebar)
			{
				sidebar.BuildItems();
			}
		}

		private void BuildItems()
		{
			var definitions = new (string Route, string Title, string Icon)[]
			{
				("DashboardPage", "Dashboard", "⌂"),
				("MainPage", "Map My Farm", "▤"),
				("LogIncidentPage", "Report Pest/Blight", "⚠"),
				("RegionalFeedPage", "Regional Feed", "〰"),
				("GeoAiPage", "GeoAI Predict", "◎"),
				("RecordsPage", "Records & Log", "▦"),
			};

			Items.Clear();
			foreach (var (route, title, icon) in definitions)
			{
				var isActive = route == ActiveRoute;
				Items.Add(new SidebarItem
				{
					Route = route,
					Title = title,
					Icon = icon,
					IsActive = isActive,
				});
			}
		}
	}

	public sealed class SidebarItem
	{
		public string Route { get; set; } = string.Empty;

		public string Title { get; set; } = string.Empty;

		public string Icon { get; set; } = string.Empty;

		public bool IsActive { get; set; }

		public Color Background => IsActive ? Color.FromArgb("#2E9E56") : Colors.Transparent;

		public Color TextColor => IsActive ? Colors.White : Color.FromArgb("#CFE0D7");

		public Color IconColor => IsActive ? Colors.White : Color.FromArgb("#9FBBAC");

		public FontAttributes FontAttributes => IsActive ? FontAttributes.Bold : FontAttributes.None;
	}
}
