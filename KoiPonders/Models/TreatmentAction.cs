namespace KoiPonders.Models
{
	/// <summary>
	/// A treatment or corrective action taken in response to a report, with follow-up tracking.
	/// </summary>
	public sealed class TreatmentAction
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// Description of the action taken (e.g. "Applied neem oil", "Removed infected leaves").
		/// </summary>
		public string Action { get; set; } = string.Empty;

		/// <summary>
		/// Product or method applied, if any.
		/// </summary>
		public string Product { get; set; } = string.Empty;

		public string PerformedBy { get; set; } = string.Empty;

		public DateTimeOffset PerformedUtc { get; set; } = DateTimeOffset.UtcNow;

		public bool WasEffective { get; set; }

		public string Outcome { get; set; } = string.Empty;
	}
}
