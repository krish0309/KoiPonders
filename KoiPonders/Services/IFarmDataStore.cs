using KoiPonders.Models;

namespace KoiPonders.Services
{
	/// <summary>
	/// Persists the farm aggregate (fields and their reports) on the device.
	/// </summary>
	public interface IFarmDataStore
	{
		/// <summary>
		/// Loads the farm from storage, creating an empty one on first run.
		/// </summary>
		Task<Farm> GetFarmAsync();

		/// <summary>
		/// Persists the current farm state to storage.
		/// </summary>
		Task SaveAsync();

		Task<IReadOnlyList<PlantField>> GetFieldsAsync();

		Task<PlantField?> GetFieldAsync(Guid fieldId);

		Task UpsertFieldAsync(PlantField field);

		Task DeleteFieldAsync(Guid fieldId);

		/// <summary>
		/// Returns all reports across all fields, newest first, paired with their owning field.
		/// </summary>
		Task<IReadOnlyList<(PlantField Field, PestDiseaseReport Report)>> GetAllReportsAsync();

		Task UpsertReportAsync(Guid fieldId, PestDiseaseReport report);

		Task DeleteReportAsync(Guid fieldId, Guid reportId);
	}
}
