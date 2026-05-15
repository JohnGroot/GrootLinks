using System.ComponentModel;
using GrootLinks.Models;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class SaveLinkTool
{
    [McpServerTool(Name = "save_link", ReadOnly = false)]
    [Description("Save a URL to the links vault with AI-powered tag classification. The link is fetched, classified, and saved as an Obsidian markdown file with needs_review: true.")]
    public async Task<string> SaveLink(
        LinkParser parser,
        TagClassifier classifier,
        VaultWriter writer,
        [Description("The URL to save")] string url,
        [Description("Optional manual tags to include (in addition to AI-suggested tags)")] string[]? tags = null,
        [Description("Optional title override (otherwise extracted from page)")] string? title = null)
    {
        var page = await parser.FetchAndParseAsync(url);
        var aiTags = await classifier.ClassifyAsync(page.Title, page.Description, page.BodyText);

        var allTags = new HashSet<string>(aiTags);
        if (tags != null)
            foreach (var t in tags)
                allTags.Add(t);

        var link = new Link
        {
            Title = title ?? page.Title,
            Url = url,
            Tags = [.. allTags],
            Source = "mcp",
            NeedsReview = true,
            Description = page.Description
        };

        var filePath = await writer.WriteLinkAsync(link);
        if (filePath == null)
            return $"Link already exists in vault for URL: {url}";

        return $"Saved: {filePath}\nTags: {string.Join(", ", link.Tags)}\nStatus: needs_review";
    }
}
