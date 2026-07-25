using System.Collections.ObjectModel;

namespace KoiPonders.ViewModels
{
	public sealed class RiskFactor
	{
		public string Name { get; set; } = string.Empty;

		public string Weight { get; set; } = string.Empty;

		public double Fraction { get; set; }

		public string Color { get; set; } = "#2E9E56";
	}

	public sealed class ProtocolStep
	{
		public string Text { get; set; } = string.Empty;
	}

	/// <summary>
	/// Backs the "GeoAI Predictive Epidemiology" screen: outbreak probability and contributing factors.
	/// </summary>
	public sealed class GeoAiViewModel
	{
		public GeoAiViewModel()
		{
			RiskFactors.Add(new RiskFactor { Name = "Humidity Index", Weight = "High", Fraction = 0.88, Color = "#B71C1C" });
			RiskFactors.Add(new RiskFactor { Name = "Canopy Density", Weight = "High", Fraction = 0.79, Color = "#E1A100" });
			RiskFactors.Add(new RiskFactor { Name = "Neighboring Outbreaks", Weight = "Moderate", Fraction = 0.61, Color = "#E1A100" });
			RiskFactors.Add(new RiskFactor { Name = "Soil Moisture", Weight = "Moderate", Fraction = 0.54, Color = "#2E9E56" });

			Protocols.Add(new ProtocolStep { Text = "Deploy targeted fungicide within the 48-hour window." });
			Protocols.Add(new ProtocolStep { Text = "Increase canopy airflow via selective pruning." });
			Protocols.Add(new ProtocolStep { Text = "Schedule GeoAI re-scan in 72 hours." });
		}

		public string OutbreakProbability { get; } = "87.4%";

		public string ForecastWindow { get; } = "Next 7 days · Corn Leaf Blight";

		public ObservableCollection<RiskFactor> RiskFactors { get; } = new();

		public ObservableCollection<ProtocolStep> Protocols { get; } = new();
	}
}
