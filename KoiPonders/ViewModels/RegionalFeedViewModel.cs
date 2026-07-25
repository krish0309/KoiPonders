using System.Collections.ObjectModel;

namespace KoiPonders.ViewModels
{
	public sealed class RegionalAlert
	{
		public string Title { get; set; } = string.Empty;

		public string Location { get; set; } = string.Empty;

		public string Distance { get; set; } = string.Empty;

		public string Severity { get; set; } = string.Empty;

		public string SeverityColor { get; set; } = "#E1A100";

		public string TimeAgo { get; set; } = string.Empty;
	}

	/// <summary>
	/// Backs the "Regional Incident Map &amp; Neighbor Feed" screen with active regional alerts.
	/// </summary>
	public sealed class RegionalFeedViewModel
	{
		public RegionalFeedViewModel()
		{
			Alerts.Add(new RegionalAlert { Title = "Northern Corn Leaf Blight", Location = "Cedar Hollow Farms", Distance = "3.2 mi NW", Severity = "HIGH", SeverityColor = "#B71C1C", TimeAgo = "2h ago" });
			Alerts.Add(new RegionalAlert { Title = "Soybean Aphid Cluster", Location = "Whitewater Co-op", Distance = "5.8 mi E", Severity = "MODERATE", SeverityColor = "#E1A100", TimeAgo = "6h ago" });
			Alerts.Add(new RegionalAlert { Title = "Tar Spot Detected", Location = "Prairie Line AgriGroup", Distance = "8.1 mi S", Severity = "HIGH", SeverityColor = "#B71C1C", TimeAgo = "1d ago" });
			Alerts.Add(new RegionalAlert { Title = "Southern Rust Suspected", Location = "Millbrook Estate", Distance = "11.4 mi SW", Severity = "LOW", SeverityColor = "#2E9E56", TimeAgo = "2d ago" });
			Alerts.Add(new RegionalAlert { Title = "Fall Armyworm Activity", Location = "Grover Family Farm", Distance = "13.0 mi SE", Severity = "MODERATE", SeverityColor = "#E1A100", TimeAgo = "3d ago" });
		}

		public ObservableCollection<RegionalAlert> Alerts { get; } = new();
	}
}
