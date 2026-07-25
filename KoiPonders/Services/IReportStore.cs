using KoiPonders.Models;

namespace KoiPonders.Services
{
	/// <summary>
	/// Persists pest/disease reports on the device. Reports are located directly on the
	/// map (via the latitude/longitude captured when the user taps the map) rather than
	/// being scoped to a field, so this store keeps a flat collection of reports.
	/// </summary>
	public interface IReportStore
	{
		/// <summary>
		/// Returns all reports, newest observation first.
		/// </summary>
		Task<IReadOnlyList<PestDiseaseReport>> GetReportsAsync();

		/// <summary>
		/// Returns a single report by id, or <c>null</c> if it does not exist.
		/// </summary>
		Task<PestDiseaseReport?> GetReportAsync(Guid reportId);

		/// <summary>
		/// Inserts a new report or updates an existing one (matched by <see cref="PestDiseaseReport.Id"/>).
		/// </summary>
		Task UpsertReportAsync(PestDiseaseReport report);

		/// <summary>
		/// Removes a report by id, if present.
		/// </summary>
		Task DeleteReportAsync(Guid reportId);
	}
}
