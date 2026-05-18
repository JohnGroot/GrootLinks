using System.Text;
using System.Text.RegularExpressions;
using GrootLinks.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GrootLinks.Services;

public partial class VaultWriter
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly string _vaultDir;
    private readonly string _linksDir;
    private readonly HashSet<string> _knownUrls = [];
    private bool _urlIndexLoaded;

    public VaultWriter(string vaultDir)
    {
        _vaultDir = vaultDir;
        _linksDir = Path.Combine(vaultDir, "links");
    }

    public async Task<string?> WriteLinkAsync(Link link)
    {
        await EnsureUrlIndexAsync();
        if (_knownUrls.Contains(link.Url)) return null;

        var yearDir = Path.Combine(_linksDir, link.Created.Year.ToString());
        Directory.CreateDirectory(yearDir);

        var slug = Slugify(link.Title);
        var filePath = Path.Combine(yearDir, $"{slug}.md");

        if (File.Exists(filePath))
        {
            var counter = 2;
            while (File.Exists(Path.Combine(yearDir, $"{slug}-{counter}.md")))
                counter++;
            filePath = Path.Combine(yearDir, $"{slug}-{counter}.md");
        }

        var content = BuildMarkdown(link);
        await File.WriteAllTextAsync(filePath, content);

        _knownUrls.Add(link.Url);
        link.FilePath = filePath;
        return filePath;
    }

    public async Task<Link?> ReadLinkAsync(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(_linksDir + Path.DirectorySeparatorChar))
            throw new ArgumentException("File path must be inside the vault links directory.");
        if (!File.Exists(fullPath)) return null;

        var content = await File.ReadAllTextAsync(fullPath);
        return ParseMarkdown(content, fullPath);
    }

    public async Task UpdateLinkTagsAsync(string filePath, List<string> newTags, bool clearReview = false)
    {
        var link = await ReadLinkAsync(filePath);
        if (link == null) throw new FileNotFoundException("Link file not found.", filePath);

        link.Tags = newTags;
        if (clearReview) link.NeedsReview = false;

        var content = BuildMarkdown(link);
        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task<List<Link>> SearchLinksAsync(string? query = null, List<string>? tags = null)
    {
        var results = new List<Link>();
        if (!Directory.Exists(_linksDir)) return results;

        foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
        {
            var link = await ReadLinkAsync(file);
            if (link == null) continue;

            if (query != null && !link.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            if (tags != null && tags.Count > 0 && !tags.Any(t => link.Tags.Contains(t)))
                continue;

            results.Add(link);
        }

        return results;
    }

    public async Task<List<Link>> GetReviewQueueAsync(int limit = 50)
    {
        var results = new List<Link>();
        if (!Directory.Exists(_linksDir)) return results;

        foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
        {
            if (results.Count >= limit) break;
            var link = await ReadLinkAsync(file);
            if (link is { NeedsReview: true })
                results.Add(link);
        }

        return results;
    }

    private async Task EnsureUrlIndexAsync()
    {
        if (_urlIndexLoaded) return;
        if (Directory.Exists(_linksDir))
        {
            foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
            {
                var link = await ReadLinkAsync(file);
                if (link != null)
                    _knownUrls.Add(link.Url);
            }
        }
        _urlIndexLoaded = true;
    }

    private static string BuildMarkdown(Link link)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{link.Title.Replace("\"", "\\\"")}\"");
        sb.AppendLine($"url: \"{link.Url}\"");
        if (link.Tags.Count > 0)
        {
            sb.AppendLine("tags:");
            foreach (var tag in link.Tags)
                sb.AppendLine($"  - {tag}");
        }
        else
        {
            sb.AppendLine("tags: []");
        }
        sb.AppendLine($"created: {link.Created:yyyy-MM-dd}");
        sb.AppendLine($"source: {link.Source}");
        sb.AppendLine($"needs_review: {(link.NeedsReview ? "true" : "false")}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {link.Title}");
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            sb.AppendLine();
            sb.AppendLine(link.Description);
        }

        return sb.ToString();
    }

    private static Link? ParseMarkdown(string content, string filePath)
    {
        var frontmatterMatch = FrontmatterRegex().Match(content);
        if (!frontmatterMatch.Success) return null;

        var yaml = frontmatterMatch.Groups[1].Value;
        var dict = YamlDeserializer.Deserialize<Dictionary<string, object?>>(yaml);
        if (dict == null) return null;

        var tags = new List<string>();
        if (dict.TryGetValue("tags", out var tagsObj) && tagsObj is List<object> tagList)
            tags = tagList.Select(t => t.ToString()!).ToList();

        var created = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dict.TryGetValue("created", out var createdObj) && createdObj != null)
        {
            if (createdObj is DateTime dt)
                created = DateOnly.FromDateTime(dt);
            else if (DateOnly.TryParse(createdObj.ToString(), out var parsed))
                created = parsed;
        }

        var needsReview = false;
        if (dict.TryGetValue("needs_review", out var nrObj) && nrObj != null)
            needsReview = nrObj is bool b ? b : string.Equals(nrObj.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        return new Link
        {
            Title = dict.GetValueOrDefault("title")?.ToString() ?? "",
            Url = dict.GetValueOrDefault("url")?.ToString() ?? "",
            Tags = tags,
            Created = created,
            Source = dict.GetValueOrDefault("source")?.ToString() ?? "unknown",
            NeedsReview = needsReview,
            Description = ExtractDescription(content),
            FilePath = filePath
        };
    }

    private static string? ExtractDescription(string content)
    {
        var afterFrontmatter = FrontmatterRegex().Replace(content, "").Trim();
        var lines = afterFrontmatter.Split('\n').Select(l => l.Trim()).Where(l => !l.StartsWith('#') && l.Length > 0).ToList();
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    internal static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = SlugStripRegex().Replace(slug, "");
        slug = SlugWhitespaceRegex().Replace(slug, "-");
        slug = SlugMultiDashRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        return slug;
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"[^\w\s-]")]
    private static partial Regex SlugStripRegex();

    [GeneratedRegex(@"[\s]+")]
    private static partial Regex SlugWhitespaceRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex SlugMultiDashRegex();
}
