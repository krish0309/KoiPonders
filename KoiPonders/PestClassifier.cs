using System.Text;
using System.Text.Json;

namespace KoiPonders;

/// <summary>A single predicted outbreak location produced by the spread model.</summary>
public class SpreadPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Intensity { get; set; } = 1;   // 1 (low) .. 5 (severe)
    public int DayOffset { get; set; }         // days from now the outbreak is expected
}

/// <summary>Result of an AI spread projection for a single blight type.</summary>
public class SpreadForecast
{
    public string BlightType { get; set; } = "";
    public string Narrative { get; set; } = "";
    public List<SpreadPoint> Points { get; } = new();
}

public static class PestClassifier
{
    public static bool UseStub = false;

    private const string ChatUrl = "https://ist-apim-aoai.azure-api.net/load-balancing/gpt-4o/openai/deployments/gpt-4o/chat/completions?api-version=2024-10-21";
    private const string ApiKey = "REMOVED_OPENAI_API_KEY";

    private const string KeyHeader = "api-key";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public static async Task<Incident?> ClassifyAsync(byte[] imageBytes)
    {
        if (UseStub) return await StubClassifyAsync(imageBytes);
        return await LiveClassifyAsync(imageBytes);
    }

    // ---------- offline path ----------

    private static readonly (string cls, string pest, string sev, string crop, string note, string treat)[] Catalog =
    {
        ("Pest Infestation", "Fall Armyworm", "HIGH", "Yellow Field Corn (Dent)",
         "Ragged feeding damage in whorl leaves; scout adjacent rows and consider targeted treatment.",
         "Apply a labelled insecticide to the whorl while larvae are small, targeting late afternoon. Re-scout in 5-7 days and treat neighbouring rows if damage advances."),
        ("Fungal Disease", "Common Rust", "MEDIUM", "Yellow Field Corn (Dent)",
         "Reddish-brown pustules on leaf surfaces; monitor humidity and canopy density.",
         "Apply a foliar fungicide if pustules reach the upper canopy before tasseling. Improve airflow by avoiding excess nitrogen and over-dense planting next season."),
        ("Crop Blight", "Northern Corn Leaf Blight", "CRITICAL", "Yellow Field Corn (Dent)",
         "Cigar-shaped grey-green lesions spreading upward; act quickly to limit yield loss.",
         "Apply a fungicide immediately — lesions above the ear leaf before silking cause serious yield loss. Plan crop rotation and a resistant hybrid for this block next season."),
        ("Pest Infestation", "Soybean Aphid", "MEDIUM", "Soybean",
         "Colonies on undersides of upper leaves; check for natural predator presence before spraying.",
         "Only treat if counts exceed roughly 250 aphids per plant and are rising. Check for lady beetles first — natural predators often collapse the colony without any spray."),
    };

    private static async Task<Incident?> StubClassifyAsync(byte[] imageBytes)
    {
        await Task.Delay(1400); // reads as real inference in the demo

        // Deterministic per-image, so the same photo always gives the same answer.
        int idx = Math.Abs(imageBytes.Length + (imageBytes.Length > 0 ? imageBytes[0] : 0))
                  % Catalog.Length;
        var c = Catalog[idx];

        return new Incident
        {
            Classification = c.cls,
            PestName = c.pest,
            Severity = c.sev,
            AffectedCrop = c.crop,
            Notes = c.note,
            Treatment = c.treat,
            Confidence = 82 + (idx * 4)
        };
    }

    // ---------- live path: photo ----------

    private static async Task<Incident?> LiveClassifyAsync(byte[] imageBytes)
    {
        try
        {
            string dataUri = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);

            var payload = new
            {
                max_tokens = 1000,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an agronomy diagnostic assistant. You always respond with a single JSON object and nothing else."
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text =
                                "Analyze this crop photo and return JSON with exactly these keys:\n" +
                                "classification: one of \"Pest Infestation\", \"Crop Blight\", \"Fungal Disease\", \"Bacterial Infection\", \"Healthy\"\n" +
                                "pestName: specific organism or disease name\n" +
                                "severity: one of \"LOW\", \"MEDIUM\", \"HIGH\", \"CRITICAL\"\n" +
                                "affectedCrop: crop species if identifiable\n" +
                                "confidence: integer 0-100\n" +
                                "notes: one sentence on visible symptoms\n" +
                                "treatment: two sentences on how to treat and contain this specific problem"
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = dataUri }
                            }
                        }
                    }
                }
            };

            return await SendAndParseAsync(payload, "PestClassifier");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PestClassifier] {ex.Message}");
            return null;
        }
    }

    // ---------- live path: plain-language description ----------

    public static async Task<Incident?> ClassifyTextAsync(string description)
    {
        if (UseStub)
        {
            await Task.Delay(1200);
            var c = Catalog[Math.Abs(description.Length) % Catalog.Length];
            return new Incident
            {
                Classification = c.cls,
                PestName = c.pest,
                Severity = c.sev,
                AffectedCrop = c.crop,
                Notes = c.note,
                Treatment = c.treat,
                Confidence = 74
            };
        }

        try
        {
            var payload = new
            {
                max_tokens = 1000,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an agronomy diagnostic assistant. You always respond with a single JSON object and nothing else."
                    },
                    new
                    {
                        role = "user",
                        content =
                            "A farmer describes a problem in their field. Identify the likely cause.\n\n" +
                            "Farmer's description: \"" + description + "\"\n\n" +
                            "Return JSON with exactly these keys:\n" +
                            "classification: one of \"Pest Infestation\", \"Crop Blight\", \"Fungal Disease\", \"Bacterial Infection\", \"Healthy\"\n" +
                            "pestName: most likely organism or disease name\n" +
                            "severity: one of \"LOW\", \"MEDIUM\", \"HIGH\", \"CRITICAL\"\n" +
                            "affectedCrop: crop species if mentioned or inferable, otherwise \"Unknown\"\n" +
                            "confidence: integer 0-100, lower for a vague description\n" +
                            "notes: one sentence restating the symptoms in agronomic terms\n" +
                            "treatment: two sentences on how to treat and contain this specific problem"
                    }
                }
            };

            return await SendAndParseAsync(payload, "ClassifyText");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClassifyText] {ex.Message}");
            return null;
        }
    }

    // ---------- shared request/parse ----------

    private static async Task<Incident?> SendAndParseAsync(object payload, string tag)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
        req.Headers.Add(KeyHeader, ApiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            System.Diagnostics.Debug.WriteLine($"[{tag}] {resp.StatusCode}: {body}");
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "";

        text = text.Replace("```json", "").Replace("```", "").Trim();

        using var result = JsonDocument.Parse(text);
        var r = result.RootElement;

        return new Incident
        {
            Classification = Get(r, "classification", "Unknown"),
            PestName = Get(r, "pestName", "Unidentified"),
            Severity = Get(r, "severity", "MEDIUM").ToUpperInvariant(),
            AffectedCrop = Get(r, "affectedCrop", "Unknown"),
            Notes = Get(r, "notes", ""),
            Treatment = Get(r, "treatment", ""),
            Confidence = r.TryGetProperty("confidence", out var c)
                               && c.TryGetInt32(out var ci) ? ci : 0
        };
    }

    // ---------- dashboard summary ----------

    public static async Task<string> SummarizeAsync(IEnumerable<Incident> incidents)
    {
        var list = incidents.ToList();
        if (list.Count == 0) return "No incidents logged yet.";

        var digest = string.Join("\n", list.Select(i =>
            $"- {i.ReportDate:MMM dd}: {i.PestName} ({i.Classification}), " +
            $"severity {i.Severity}, field {i.FieldName}, crop {i.AffectedCrop}"));

        if (UseStub)
        {
            await Task.Delay(1200);
            var worst = list.OrderByDescending(i => i.Severity == "CRITICAL" ? 3
                                                  : i.Severity == "HIGH" ? 2 : 1).First();
            return $"{list.Count} incident(s) logged across " +
                   $"{list.Select(i => i.FieldName).Distinct().Count()} field(s). " +
                   $"Highest concern is {worst.PestName} at {worst.Severity} severity in {worst.FieldName}. " +
                   $"Suggested actions: scout adjacent parcels within the spread radius, apply the recommended treatment to affected plants, " +
                   $"isolate and sanitize to limit further spread, and re-inspect the site in a few days to confirm the response is working.";
        }

        try
        {
            var payload = new
            {
                max_tokens = 500,
                temperature = 0.3,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an agronomy analyst. Write a clear, verbose brief in 4-6 sentences. No markdown, no bullet points."
                    },
                    new
                    {
                        role = "user",
                        content =
                            "Summarize these farm incident records for a dashboard. " +
                            "Note any pattern and the most urgent threat, then give the grower several specific, " +
                            "actionable suggestions for how to respond to these challenges " +
                            "(such as scouting, treatment, containment, and follow-up monitoring).\n\n" + digest
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            req.Headers.Add(KeyHeader, ApiKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Summarize] {resp.StatusCode}: {body}");
                return "Summary unavailable — check connection.";
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content").GetString()
                   ?? "Summary unavailable.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Summarize] {ex.Message}");
            return "Summary unavailable.";
        }
    }

    private static string Get(JsonElement e, string prop, string fallback) =>
        e.TryGetProperty(prop, out var v) ? v.GetString() ?? fallback : fallback;

    // ---------- outbreak spread projection ----------

    /// <summary>
    /// Asks the LLM to project where a specific blight/pest will spread over the coming
    /// days, based on the incidents already reported for that type and the crops growing
    /// in the surrounding fields. Returns a set of weighted points for a heatmap layer.
    /// </summary>
    public static async Task<SpreadForecast> PredictSpreadAsync(
        string blightType,
        IEnumerable<Incident> incidents,
        IEnumerable<Parcel> parcels)
    {
        var forecast = new SpreadForecast { BlightType = blightType };

        var sources = incidents
            .Where(i => i.Location is not null)
            .ToList();

        if (sources.Count == 0)
        {
            forecast.Narrative = "No located reports for this blight type yet.";
            return forecast;
        }

        // Describe the confirmed outbreaks and the crops at risk nearby.
        var incidentDigest = string.Join("\n", sources.Select(i =>
            $"- {i.PestName} ({i.Severity}) at lat {i.Location!.Y:F5}, lon {i.Location.X:F5} " +
            $"in field {i.FieldName}, crop {i.AffectedCrop}, reported {i.ReportDate:MMM dd}"));

        var cropDigest = string.Join("\n", parcels
            .Where(p => p.Geometry?.Extent is not null)
            .Select(p =>
            {
                var c = p.Geometry!.Extent!.GetCenter();
                return $"- Field {p.Name}: crop {p.Crop}, {p.Acres:F1} acres, " +
                       $"center lat {c.Y:F5}, lon {c.X:F5}";
            }));

        if (UseStub)
        {
            await Task.Delay(1200);
            var rnd = new Random(blightType.Length + sources.Count);
            foreach (var s in sources)
            {
                for (int d = 1; d <= 3; d++)
                {
                    forecast.Points.Add(new SpreadPoint
                    {
                        Latitude = s.Location!.Y + (rnd.NextDouble() - 0.5) * 0.0015 * d,
                        Longitude = s.Location.X + (rnd.NextDouble() - 0.5) * 0.0015 * d,
                        Intensity = Math.Max(1, 5 - d),
                        DayOffset = d * 2
                    });
                }
            }
            forecast.Narrative =
                $"{blightType} is projected to expand outward from {sources.Count} confirmed " +
                "site(s) over the next week, favouring adjacent susceptible crops.";
            return forecast;
        }

        try
        {
            var payload = new
            {
                max_tokens = 1200,
                temperature = 0.3,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an agronomy epidemiology model. You forecast how a " +
                                  "plant pest or disease will spread across farmland. You always " +
                                  "respond with a single JSON object and nothing else."
                    },
                    new
                    {
                        role = "user",
                        content =
                            $"Blight/pest to model: \"{blightType}\".\n\n" +
                            "Confirmed outbreak reports:\n" + incidentDigest + "\n\n" +
                            "Nearby fields and their crops:\n" + cropDigest + "\n\n" +
                            "Project where this outbreak is likely to be found over the next 7 days. " +
                            "Consider proximity to confirmed sites, severity, and whether nearby " +
                            "fields grow a susceptible crop. Return JSON with exactly these keys:\n" +
                            "narrative: two sentences describing the expected spread direction and the most at-risk crops/fields\n" +
                            "points: an array of 8-20 objects, each with keys " +
                            "\"lat\" (number), \"lon\" (number), \"intensity\" (integer 1-5, higher = more likely/severe), " +
                            "\"day\" (integer 0-7, days from now). Keep all coordinates within roughly 0.004 degrees "  +
                            "of the confirmed report locations so the projection stays tight around the outbreak."
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            req.Headers.Add(KeyHeader, ApiKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[PredictSpread] {resp.StatusCode}: {body}");
                forecast.Narrative = "Spread projection unavailable — check connection.";
                return forecast;
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "";

            text = text.Replace("```json", "").Replace("```", "").Trim();

            using var result = JsonDocument.Parse(text);
            var r = result.RootElement;

            forecast.Narrative = Get(r, "narrative", "Spread projection generated.");

            if (r.TryGetProperty("points", out var pts) && pts.ValueKind == JsonValueKind.Array)
            {
                // Constrain every projected point to a tight radius around the nearest
                // confirmed incident so the heatmap stays a realistic representation of
                // spread and never trails far off toward empty terrain.
                const double maxDegrees = 0.004; // ~450 m

                foreach (var p in pts.EnumerateArray())
                {
                    double lat = p.TryGetProperty("lat", out var la) && la.TryGetDouble(out var lav) ? lav : double.NaN;
                    double lon = p.TryGetProperty("lon", out var lo) && lo.TryGetDouble(out var lov) ? lov : double.NaN;
                    if (double.IsNaN(lat) || double.IsNaN(lon)) continue;

                    // Snap the point back toward the closest confirmed report if it
                    // drifted beyond the allowed radius.
                    var nearest = sources
                        .OrderBy(s => ((s.Location!.Y - lat) * (s.Location.Y - lat)) +
                                      ((s.Location.X - lon) * (s.Location.X - lon)))
                        .First().Location!;

                    double dLat = lat - nearest.Y;
                    double dLon = lon - nearest.X;
                    double dist = Math.Sqrt((dLat * dLat) + (dLon * dLon));
                    if (dist > maxDegrees && dist > 0)
                    {
                        double scale = maxDegrees / dist;
                        lat = nearest.Y + (dLat * scale);
                        lon = nearest.X + (dLon * scale);
                    }

                    int intensity = p.TryGetProperty("intensity", out var it) && it.TryGetInt32(out var iv)
                        ? Math.Clamp(iv, 1, 5) : 1;
                    int day = p.TryGetProperty("day", out var dy) && dy.TryGetInt32(out var dv)
                        ? Math.Clamp(dv, 0, 7) : 0;

                    forecast.Points.Add(new SpreadPoint
                    {
                        Latitude = lat,
                        Longitude = lon,
                        Intensity = intensity,
                        DayOffset = day
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PredictSpread] {ex.Message}");
            if (string.IsNullOrEmpty(forecast.Narrative))
                forecast.Narrative = "Spread projection unavailable.";
        }

        return forecast;
    }
}