using System.Text.Json.Serialization;

namespace KoiPonders.Models
{
	/// <summary>
	/// A field/plot on the farm defined by a polygon drawn or GPS-captured on the map.
	/// </summary>
	public sealed class PlantField
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// The crop or plant type grown in this field.
		/// </summary>
		public string CropType { get; set; } = string.Empty;

		public string Notes { get; set; } = string.Empty;

		/// <summary>
		/// ArcGIS geometry serialized via <c>Geometry.ToJson()</c> so it round-trips exactly.
		/// </summary>
		public string? GeometryJson { get; set; }

		/// <summary>
		/// Cached area in hectares, computed when the field is saved.
		/// </summary>
		public double AreaHectares { get; set; }

		public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

		public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

		/// <summary>
		/// Reports filed against this field.
		/// </summary>
		public List<PestDiseaseReport> Reports { get; set; } = new();

		[JsonIgnore]
		public int OpenReportCount => Reports.Count(r =>
			r.Status != ReportStatus.Resolved);
	}
}
