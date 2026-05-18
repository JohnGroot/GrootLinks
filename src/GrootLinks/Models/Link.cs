namespace GrootLinks.Models;

public class Link
{
    public required string Title { get; set; }
    public required string Url { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateOnly Created { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Source { get; set; } = "mcp";
    public bool NeedsReview { get; set; } = true;
    public string? Description { get; set; }
    public string? FilePath { get; set; }
}
