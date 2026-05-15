using System.ComponentModel;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class RetagLinkTool
{
    [McpServerTool(Name = "retag_link", ReadOnly = false)]
    [Description("Update tags on an existing link. Optionally clear the needs_review flag.")]
    public async Task<string> RetagLink(
        VaultWriter writer,
        TaxonomyService taxonomy,
        [Description("Path to the link markdown file")] string filePath,
        [Description("New tags to assign")] string[] tags,
        [Description("Set to true to mark the link as reviewed")] bool clearReview = false)
    {
        var invalid = tags.Where(t => !taxonomy.IsValidTag(t)).ToArray();
        if (invalid.Length > 0)
            return $"Invalid tags not in taxonomy: {string.Join(", ", invalid)}";

        await writer.UpdateLinkTagsAsync(filePath, [.. tags], clearReview);

        var link = await writer.ReadLinkAsync(filePath);
        return $"Updated: {filePath}\nTags: {string.Join(", ", link?.Tags ?? [])}\nReview: {(link?.NeedsReview == true ? "pending" : "cleared")}";
    }
}
