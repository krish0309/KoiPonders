using System.Text.Json;
using System.Text.Json.Serialization;
using KoiPonders.Models;

namespace KoiPonders.Services
{
	/// <summary>
	/// A thread-safe, file-backed <see cref="IReportStore"/> that serializes the map-located
	/// reports to a single JSON file in the app data directory. Mirrors the persistence
	/// approach used on the <c>kyle</c> branch, adapted to a flat list of reports.
	/// </summary>
	public sealed class JsonReportStore : IReportStore
	{
		private static readonly JsonSerializerOptions SerializerOptions = new()
		{
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter() }
		};

		private readonly string _filePath;
		private readonly SemaphoreSlim _gate = new(1, 1);
		private List<PestDiseaseReport>? _reports;

		public JsonReportStore()
		{
			_filePath = Path.Combine(FileSystem.AppDataDirectory, "koiponders-reports.json");
		}

		public async Task<IReadOnlyList<PestDiseaseReport>> GetReportsAsync()
		{
			var reports = await GetOrLoadAsync().ConfigureAwait(false);
			return reports
				.OrderByDescending(r => r.ObservedUtc)
				.ToList();
		}

		public async Task<PestDiseaseReport?> GetReportAsync(Guid reportId)
		{
			var reports = await GetOrLoadAsync().ConfigureAwait(false);
			return reports.FirstOrDefault(r => r.Id == reportId);
		}

		public async Task UpsertReportAsync(PestDiseaseReport report)
		{
			ArgumentNullException.ThrowIfNull(report);
			var reports = await GetOrLoadAsync().ConfigureAwait(false);

			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				var existing = reports.FirstOrDefault(r => r.Id == report.Id);
				if (existing is null)
				{
					reports.Add(report);
				}
				else if (!ReferenceEquals(existing, report))
				{
					reports[reports.IndexOf(existing)] = report;
				}

				await SaveAsync(reports).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task DeleteReportAsync(Guid reportId)
		{
			var reports = await GetOrLoadAsync().ConfigureAwait(false);

			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				var existing = reports.FirstOrDefault(r => r.Id == reportId);
				if (existing is not null)
				{
					reports.Remove(existing);
					await SaveAsync(reports).ConfigureAwait(false);
				}
			}
			finally
			{
				_gate.Release();
			}
		}

		private async Task<List<PestDiseaseReport>> GetOrLoadAsync()
		{
			if (_reports is not null)
			{
				return _reports;
			}

			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				_reports ??= await LoadFromDiskAsync().ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}

			return _reports;
		}

		private async Task SaveAsync(List<PestDiseaseReport> reports)
		{
			var json = JsonSerializer.Serialize(reports, SerializerOptions);
			await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
		}

		private async Task<List<PestDiseaseReport>> LoadFromDiskAsync()
		{
			try
			{
				if (File.Exists(_filePath))
				{
					var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
					var reports = JsonSerializer.Deserialize<List<PestDiseaseReport>>(json, SerializerOptions);
					if (reports is not null)
					{
						return reports;
					}
				}
			}
			catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
			{
				// Corrupt or unreadable store: start fresh rather than crashing the app.
			}

			return new List<PestDiseaseReport>();
		}
	}
}
