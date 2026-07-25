using System.Text.Json.Serialization;

namespace KoiPonders.Models
{
/// <summary>
/// A pest or disease observation located on the map, including evidence photos, a
/// treatment action log, and follow-up status.
/// </summary>
public sealed class PestDiseaseReport
{
public Guid Id { get; set; } = Guid.NewGuid();

/// <summary>
/// Name of the pest, disease, or problem observed (e.g. "Aphids", "Powdery mildew").
/// </summary>
public string ProblemName { get; set; } = string.Empty;

public ProblemCategory Category { get; set; } = ProblemCategory.Insect;

public Severity Severity { get; set; } = Severity.Low;

public ReportStatus Status { get; set; } = ReportStatus.Open;

/// <summary>
/// Approximate percentage of the field affected (0-100).
/// </summary>
public double AffectedAreaPercent { get; set; }

public string Notes { get; set; } = string.Empty;

/// <summary>
/// Latitude (WGS84) of the map-tapped report location, if the user placed one.
/// </summary>
public double? Latitude { get; set; }

/// <summary>
/// Longitude (WGS84) of the map-tapped report location, if the user placed one.
/// </summary>
public double? Longitude { get; set; }

[JsonIgnore]
public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

public DateTimeOffset ObservedUtc { get; set; } = DateTimeOffset.UtcNow;

public DateTimeOffset? FollowUpUtc { get; set; }

public List<ReportPhoto> Photos { get; set; } = new();

public List<TreatmentAction> Treatments { get; set; } = new();

[JsonIgnore]
public bool IsOpen => Status != ReportStatus.Resolved;
}
}
