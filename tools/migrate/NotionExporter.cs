using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrootLinks.Migrate;

public class NotionExporter : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _databaseId;

    public NotionExporter(string notionToken, string databaseId)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", notionToken);
        _http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
        _databaseId = databaseId;
    }

    public void Dispose() => _http.Dispose();

    public async Task<List<NotionEntry>> ExportAllAsync(Action<string>? log = null)
    {
        var entries = new List<NotionEntry>();
        string? cursor = null;

        var schemaReq = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.notion.com/v1/databases/{_databaseId}");
        var schemaResp = await _http.SendAsync(schemaReq);
        schemaResp.EnsureSuccessStatusCode();
        var schemaDoc = JsonDocument.Parse(await schemaResp.Content.ReadAsStringAsync());
        var props = schemaDoc.RootElement.GetProperty("properties");
        log?.Invoke($"Database properties: {string.Join(", ", props.EnumerateObject().Select(p => $"{p.Name}({p.Value.GetProperty("type").GetString()})"))}");

        while (true)
        {
            var body = new Dictionary<string, object> { ["page_size"] = 100 };
            if (cursor != null) body["start_cursor"] = cursor;

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.notion.com/v1/databases/{_databaseId}/query")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                var entryProps = result.GetProperty("properties");
                var entry = new NotionEntry
                {
                    Id = result.GetProperty("id").GetString()!,
                    Title = ExtractTitle(entryProps),
                    Url = ExtractUrl(entryProps),
                    Tags = ExtractTags(entryProps),
                    Created = ExtractCreated(entryProps)
                };
                entries.Add(entry);
            }

            log?.Invoke($"Fetched {entries.Count} entries...");

            if (!root.GetProperty("has_more").GetBoolean()) break;
            cursor = root.GetProperty("next_cursor").GetString();
        }

        return entries;
    }

    public static async Task SaveToFileAsync(List<NotionEntry> entries, string path)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static string ExtractTitle(JsonElement props)
    {
        if (props.TryGetProperty("Name", out var name) &&
            name.GetProperty("title").GetArrayLength() > 0)
        {
            return name.GetProperty("title")[0].GetProperty("plain_text").GetString() ?? "";
        }
        return "";
    }

    private static string? ExtractUrl(JsonElement props)
    {
        if (props.TryGetProperty("URL", out var url) &&
            url.GetProperty("url").ValueKind != JsonValueKind.Null)
        {
            return url.GetProperty("url").GetString();
        }
        return null;
    }

    private static List<string> ExtractTags(JsonElement props)
    {
        var tags = new List<string>();
        if (props.TryGetProperty("Tags", out var tagsEl) &&
            tagsEl.GetProperty("multi_select").ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.GetProperty("multi_select").EnumerateArray())
                tags.Add(tag.GetProperty("name").GetString()!);
        }
        return tags;
    }

    private static DateOnly ExtractCreated(JsonElement props)
    {
        if (props.TryGetProperty("Created", out var created) &&
            created.GetProperty("created_time").ValueKind != JsonValueKind.Null)
        {
            var dt = DateTime.Parse(created.GetProperty("created_time").GetString()!);
            return DateOnly.FromDateTime(dt);
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}

public class NotionEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateOnly Created { get; set; }
}
