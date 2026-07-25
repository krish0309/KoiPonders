using Esri.ArcGISRuntime.Geometry;
using System.Text.Json;

namespace KoiPonders;

public static class FarmStore
{
    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "farmguard.json");

    private class ParcelDto
    {
        public string Name { get; set; } = "";
        public double Acres { get; set; }
        public DateTime MappedDate { get; set; }
        public string GeometryJson { get; set; } = "";
    }

    private class IncidentDto
    {
        public string Classification { get; set; } = "";
        public string PestName { get; set; } = "";
        public string Severity { get; set; } = "";
        public string AffectedCrop { get; set; } = "";
        public string Status { get; set; } = "";
        public string Notes { get; set; } = "";
        public string FieldName { get; set; } = "";
        public int Confidence { get; set; }
        public DateTime ReportDate { get; set; }
        public string LocationJson { get; set; } = "";
    }

    private class Snapshot
    {
        public List<ParcelDto> Parcels { get; set; } = new();
        public List<IncidentDto> Incidents { get; set; } = new();
    }

    public static async Task SaveAsync(IEnumerable<Parcel> parcels,
                                       IEnumerable<Incident> incidents)
    {
        try
        {
            var snap = new Snapshot
            {
                Parcels = parcels.Where(p => p.Geometry is not null)
                    .Select(p => new ParcelDto
                    {
                        Name = p.Name,
                        Acres = p.Acres,
                        MappedDate = p.MappedDate,
                        GeometryJson = p.Geometry!.ToJson()
                    }).ToList(),

                Incidents = incidents.Where(i => i.Location is not null)
                    .Select(i => new IncidentDto
                    {
                        Classification = i.Classification,
                        PestName = i.PestName,
                        Severity = i.Severity,
                        AffectedCrop = i.AffectedCrop,
                        Status = i.Status,
                        Notes = i.Notes,
                        FieldName = i.FieldName,
                        Confidence = i.Confidence,
                        ReportDate = i.ReportDate,
                        LocationJson = i.Location!.ToJson()
                    }).ToList()
            };

            await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(snap));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] save failed: {ex.Message}");
        }
    }

    public static async Task<(List<Parcel> Parcels, List<Incident> Incidents)> LoadAsync()
    {
        var parcels = new List<Parcel>();
        var incidents = new List<Incident>();

        try
        {
            if (!File.Exists(FilePath)) return (parcels, incidents);

            var snap = JsonSerializer.Deserialize<Snapshot>(
                await File.ReadAllTextAsync(FilePath));
            if (snap is null) return (parcels, incidents);

            foreach (var p in snap.Parcels)
                parcels.Add(new Parcel
                {
                    Name = p.Name,
                    Acres = p.Acres,
                    MappedDate = p.MappedDate,
                    Geometry = Geometry.FromJson(p.GeometryJson)
                });

            foreach (var i in snap.Incidents)
                incidents.Add(new Incident
                {
                    Classification = i.Classification,
                    PestName = i.PestName,
                    Severity = i.Severity,
                    AffectedCrop = i.AffectedCrop,
                    Status = i.Status,
                    Notes = i.Notes,
                    FieldName = i.FieldName,
                    Confidence = i.Confidence,
                    ReportDate = i.ReportDate,
                    Location = Geometry.FromJson(i.LocationJson) as MapPoint
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FarmStore] load failed: {ex.Message}");
        }

        return (parcels, incidents);
    }
}