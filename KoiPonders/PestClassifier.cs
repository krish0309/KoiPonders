using System.Text;
using System.Text.Json;

namespace KoiPonders;

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

    private static readonly (string cls, string pest, string sev, string crop, string note)[] Catalog =
    {
        ("Pest Infestation", "Fall Armyworm", "HIGH", "Yellow Field Corn (Dent)",
         "Ragged feeding damage in whorl leaves; scout adjacent rows and consider targeted treatment."),
        ("Fungal Disease", "Common Rust", "MEDIUM", "Yellow Field Corn (Dent)",
         "Reddish-brown pustules on leaf surfaces; monitor humidity and canopy density."),
        ("Crop Blight", "Northern Corn Leaf Blight", "CRITICAL", "Yellow Field Corn (Dent)",
         "Cigar-shaped grey-green lesions spreading upward; act quickly to limit yield loss."),
        ("Pest Infestation", "Soybean Aphid", "MEDIUM", "Soybean",
         "Colonies on undersides of upper leaves; check for natural predator presence before spraying."),
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
            Confidence = 82 + (idx * 4)
        };
    }

    // ---------- live path ----------

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
                                "notes: one sentence on visible symptoms and recommended action"
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

            var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            req.Headers.Add(KeyHeader, ApiKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[PestClassifier] {resp.StatusCode}: {body}");
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
                Confidence = r.TryGetProperty("confidence", out var c)
                                   && c.TryGetInt32(out var ci) ? ci : 0
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PestClassifier] {ex.Message}");
            return null;
        }
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
                   $"Recommend scouting adjacent parcels within the spread radius.";
        }

        try
        {
            var payload = new
            {
                max_tokens = 400,
                temperature = 0.3,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an agronomy analyst. Write 2-3 concise sentences. No markdown, no bullet points."
                    },
                    new
                    {
                        role = "user",
                        content =
                            "Summarize these farm incident records for a dashboard. " +
                            "Note any pattern, the most urgent threat, and one recommended action.\n\n" + digest
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
}