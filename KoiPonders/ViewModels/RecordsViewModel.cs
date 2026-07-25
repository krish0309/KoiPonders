using System.Collections.ObjectModel;
using KoiPonders.Services;

namespace KoiPonders.ViewModels
{
	public sealed class RecordRow
	{
		public string Date { get; set; } = string.Empty;

		public string Field { get; set; } = string.Empty;

		public string Problem { get; set; } = string.Empty;

		public string Category { get; set; } = string.Empty;

		public string Severity { get; set; } = string.Empty;

		public string SeverityColor { get; set; } = "#2E9E56";

		public string Status { get; set; } = string.Empty;
	}

	/// <summary>
	/// Backs the "Records &amp; Spatial Database Logs" screen: a tabular log of all reports.
	/// </summary>
	public sealed class RecordsViewModel
	{
		private readonly IFarmDataStore _dataStore;

		public RecordsViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
		}

		public ObservableCollection<RecordRow> Rows { get; } = new();

		public async Task LoadAsync()
		{
			var reports = await _dataStore.GetAllReportsAsync();
			Rows.Clear();
			foreach (var (field, report) in reports)
			{
				Rows.Add(new RecordRow
				{
					Date = report.ObservedUtc.ToString("MMM dd, yyyy"),
					Field = field.Name,
					Problem = string.IsNullOrWhiteSpace(report.ProblemName) ? "Unnamed incident" : report.ProblemName,
					Category = report.Category.ToString(),
					Severity = report.Severity.ToString().ToUpperInvariant(),
					SeverityColor = report.Severity switch
					{
						Models.Severity.Critical => "#B71C1C",
						Models.Severity.High => "#E1A100",
						Models.Severity.Moderate => "#2E7DD1",
						_ => "#2E9E56",
					},
					Status = report.Status.ToString(),
				});
			}
		}
	}
}
