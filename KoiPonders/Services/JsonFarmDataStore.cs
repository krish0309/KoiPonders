using System.Text.Json;
using System.Text.Json.Serialization;
using KoiPonders.Models;

namespace KoiPonders.Services
{
	/// <summary>
	/// A thread-safe, file-backed <see cref="IFarmDataStore"/> that serializes the farm
	/// aggregate to a single JSON file in the app data directory.
	/// </summary>
	public sealed class JsonFarmDataStore : IFarmDataStore
	{
		private static readonly JsonSerializerOptions SerializerOptions = new()
		{
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter() }
		};

		private readonly string _filePath;
		private readonly SemaphoreSlim _gate = new(1, 1);
		private Farm? _farm;

		public JsonFarmDataStore()
		{
			_filePath = Path.Combine(FileSystem.AppDataDirectory, "koiponders-farm.json");
		}

		public async Task<Farm> GetFarmAsync()
		{
			if (_farm is not null)
			{
				return _farm;
			}

			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				if (_farm is null)
				{
					_farm = await LoadFromDiskAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				_gate.Release();
			}

			return _farm;
		}

		public async Task SaveAsync()
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				var json = JsonSerializer.Serialize(farm, SerializerOptions);
				await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<IReadOnlyList<PlantField>> GetFieldsAsync()
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			return farm.Fields
				.OrderBy(f => f.Name)
				.ToList();
		}

		public async Task<PlantField?> GetFieldAsync(Guid fieldId)
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			return farm.Fields.FirstOrDefault(f => f.Id == fieldId);
		}

		public async Task UpsertFieldAsync(PlantField field)
		{
			ArgumentNullException.ThrowIfNull(field);
			var farm = await GetFarmAsync().ConfigureAwait(false);

			var existing = farm.Fields.FirstOrDefault(f => f.Id == field.Id);
			field.UpdatedUtc = DateTimeOffset.UtcNow;
			if (existing is null)
			{
				farm.Fields.Add(field);
			}
			else if (!ReferenceEquals(existing, field))
			{
				farm.Fields[farm.Fields.IndexOf(existing)] = field;
			}

			await SaveAsync().ConfigureAwait(false);
		}

		public async Task DeleteFieldAsync(Guid fieldId)
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			var existing = farm.Fields.FirstOrDefault(f => f.Id == fieldId);
			if (existing is not null)
			{
				farm.Fields.Remove(existing);
				await SaveAsync().ConfigureAwait(false);
			}
		}

		public async Task<IReadOnlyList<(PlantField Field, PestDiseaseReport Report)>> GetAllReportsAsync()
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			return farm.Fields
				.SelectMany(f => f.Reports.Select(r => (Field: f, Report: r)))
				.OrderByDescending(x => x.Report.ObservedUtc)
				.ToList();
		}

		public async Task UpsertReportAsync(Guid fieldId, PestDiseaseReport report)
		{
			ArgumentNullException.ThrowIfNull(report);
			var farm = await GetFarmAsync().ConfigureAwait(false);
			var field = farm.Fields.FirstOrDefault(f => f.Id == fieldId)
				?? throw new InvalidOperationException($"Field '{fieldId}' was not found.");

			var existing = field.Reports.FirstOrDefault(r => r.Id == report.Id);
			if (existing is null)
			{
				field.Reports.Add(report);
			}
			else if (!ReferenceEquals(existing, report))
			{
				field.Reports[field.Reports.IndexOf(existing)] = report;
			}

			await SaveAsync().ConfigureAwait(false);
		}

		public async Task DeleteReportAsync(Guid fieldId, Guid reportId)
		{
			var farm = await GetFarmAsync().ConfigureAwait(false);
			var field = farm.Fields.FirstOrDefault(f => f.Id == fieldId);
			var report = field?.Reports.FirstOrDefault(r => r.Id == reportId);
			if (field is not null && report is not null)
			{
				field.Reports.Remove(report);
				await SaveAsync().ConfigureAwait(false);
			}
		}

		private async Task<Farm> LoadFromDiskAsync()
		{
			try
			{
				if (File.Exists(_filePath))
				{
					var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
					var farm = JsonSerializer.Deserialize<Farm>(json, SerializerOptions);
					if (farm is not null)
					{
						return farm;
					}
				}
			}
			catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
			{
				// Corrupt or unreadable store: start fresh rather than crashing the app.
			}

			return new Farm();
		}
	}
}
