namespace KoiPonders.Models
{
	/// <summary>
	/// Severity of an observed pest or disease occurrence.
	/// </summary>
	public enum Severity
	{
		Low,
		Moderate,
		High,
		Critical
	}

	/// <summary>
	/// Follow-up lifecycle status of a pest/disease report.
	/// </summary>
	public enum ReportStatus
	{
		Open,
		Monitoring,
		Treating,
		Resolved,
		Escalated
	}

	/// <summary>
	/// High-level classification of the problem being reported.
	/// </summary>
	public enum ProblemCategory
	{
		Insect,
		Fungal,
		Bacterial,
		Viral,
		Weed,
		NutrientDeficiency,
		Environmental,
		Other
	}
}
