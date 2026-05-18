using System.ComponentModel;
using System.Text;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class ReviewQueueTool
{
    [McpServerTool(Name = "review_queue", ReadOnly = true)]
    [Description("List links that need tag review (needs_review: true).")]
    public async Task<string> ReviewQueue(
        VaultWriter writer,
        [Description("Maximum number of links to return")] int limit = 20)
    {
        var queue = await writer.GetReviewQueueAsync(limit);

        if (queue.Count == 0)
            return "No links pending review.";

        var sb = new StringBuilder();
        sb.AppendLine($"{queue.Count} link(s) pending review:");
        foreach (var link in queue)
        {
            sb.AppendLine($"- **{link.Title}**");
            sb.AppendLine($"  File: {link.FilePath}");
            sb.AppendLine($"  Tags: {string.Join(", ", link.Tags)}");
        }

        return sb.ToString();
    }
}
