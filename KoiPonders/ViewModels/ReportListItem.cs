using KoiPonders.Models;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Flattened view of a report together with the field it belongs to, for list display.
	/// </summary>
	public sealed class ReportListItem
	{
		public ReportListItem(PlantField field, PestDiseaseReport report)
		{
			Field = field;
			Report = report;
		}

		public PlantField Field { get; }

		public PestDiseaseReport Report { get; }

		public string FieldName => Field.Name;

		public string ProblemName => Report.ProblemName;

		public string SeverityText => $"{Report.Severity} severity";

		public string StatusText => Report.Status.ToString();

		public string Summary =>
			$"{Report.Category} • {Report.AffectedAreaPercent:0}% affected • {Report.ObservedUtc.LocalDateTime:d}";

		public Color StatusColor => Report.Status switch
		{
			ReportStatus.Resolved => Color.FromArgb("#2E7D32"),
			ReportStatus.Escalated => Color.FromArgb("#B71C1C"),
			ReportStatus.Treating => Color.FromArgb("#EF6C00"),
			ReportStatus.Monitoring => Color.FromArgb("#1565C0"),
			_ => Color.FromArgb("#616161")
		};
	}
}
