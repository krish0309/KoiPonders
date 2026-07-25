namespace KoiPonders.Models
{
	/// <summary>
	/// The top-level farm aggregate that owns all fields (and, through them, reports).
	/// Serialized as a single JSON document on the device.
	/// </summary>
	public sealed class Farm
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public string Name { get; set; } = "My Farm";

		public List<PlantField> Fields { get; set; } = new();
	}
}
