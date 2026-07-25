namespace KoiPonders.Models
{
	/// <summary>
	/// A photo attached to a report. The image file is copied into app data and referenced by path.
	/// </summary>
	public sealed class ReportPhoto
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// Absolute path to the stored image file within the app data directory.
		/// </summary>
		public string FilePath { get; set; } = string.Empty;

		public string Caption { get; set; } = string.Empty;

		public DateTimeOffset CapturedUtc { get; set; } = DateTimeOffset.UtcNow;
	}
}
