using System.ComponentModel;
using System.Text;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class SearchLinksTool
{
    [McpServerTool(Name = "search_links", ReadOnly = true)]
    [Description("Search saved links by text query and/or tags.")]
    public async Task<string> SearchLinks(
        VaultWriter writer,
        [Description("Text to search for in link titles")] string? query = null,
        [Description("Filter by these tags (returns links matching ANY tag)")] string[]? tags = null)
    {
        var results = await writer.SearchLinksAsync(query, tags?.ToList());

        if (results.Count == 0)
            return "No links found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} link(s):");
        foreach (var link in results)
        {
            sb.AppendLine($"- **{link.Title}**");
            sb.AppendLine($"  URL: {link.Url}");
            sb.AppendLine($"  Tags: {string.Join(", ", link.Tags)}");
            sb.AppendLine($"  Review: {(link.NeedsReview ? "pending" : "done")}");
        }

        return sb.ToString();
    }
}
