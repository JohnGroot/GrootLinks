using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace GrootLinks.Migrate;

public class TagAnalyzer
{
    public static TagAnalysisReport Analyze(List<NotionEntry> entries)
    {
        var tagCounts = new Dictionary<string, int>();
        var untaggedCount = 0;

        foreach (var entry in entries)
        {
            if (entry.Tags.Count == 0) { untaggedCount++; continue; }
            foreach (var tag in entry.Tags)
                tagCounts[tag] = tagCounts.GetValueOrDefault(tag) + 1;
        }

        var sorted = tagCounts.OrderByDescending(kv => kv.Value).ToList();

        return new TagAnalysisReport
        {
            TotalEntries = entries.Count,
            TaggedEntries = entries.Count - untaggedCount,
            UntaggedEntries = untaggedCount,
            UniqueTagCount = tagCounts.Count,
            TagFrequencies = sorted.Select(kv => new TagFrequency(kv.Key, kv.Value)).ToList()
        };
    }

    public static async Task<string> SuggestTaxonomyExpansionAsync(
        TagAnalysisReport report,
        string currentTaxonomyJson,
        AnthropicClient client)
    {
        var tagList = string.Join("\n", report.TagFrequencies.Select(t => $"- {t.Tag} ({t.Count} uses)"));

        var prompt = $"""
            You are expanding a tag taxonomy to accommodate existing tags from a links database.

            ## Current Taxonomy
            {currentTaxonomyJson}

            ## All Existing Tags (with frequency)
            {tagList}

            Analyze these tags and produce an EXPANDED version of the taxonomy JSON that:
            1. Adds new subcategories and leaf tags to accommodate ALL existing tags
            2. Preserves the existing structure — don't remove or rename existing entries
            3. Uses kebab-case for all tag slugs
            4. Allows 3-4 levels of nesting where it makes sense
            5. Groups related tags logically (e.g., person names under relevant topics)
            6. Merges obvious duplicates/typos into one canonical tag

            Return ONLY the expanded taxonomy JSON. No explanation.
            """;

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_5,
            MaxTokens = 8192,
            Messages = [new MessageParam { Role = Role.User, Content = prompt }]
        });

        var text = "";
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                text = textBlock.Text;
        }

        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            text = text[jsonStart..(jsonEnd + 1)];

        return text;
    }

    public static async Task<Dictionary<string, string>> GenerateAliasesAsync(
        TagAnalysisReport report,
        string taxonomyJson,
        AnthropicClient client)
    {
        var tagList = string.Join("\n", report.TagFrequencies.Select(t => $"- {t.Tag} ({t.Count} uses)"));

        var prompt = $"""
            Map old Notion tags to canonical slugs from this taxonomy.

            ## Taxonomy
            {taxonomyJson}

            ## Old Tags (with frequency)
            {tagList}

            For EVERY old tag, provide a mapping to the best matching tag slug in the taxonomy.
            Rules:
            - Fix typos (e.g., "Theroy" -> correct tag)
            - Merge duplicates (e.g., "TikTook" and "TikTok" -> same slug)
            - Normalize to kebab-case
            - Map person names to their most relevant topic tag
            - Prefer specific leaf tags over broad parents

            Return ONLY a JSON object mapping old tag names to new slugs. No explanation.
            """;

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_5,
            MaxTokens = 8192,
            Messages = [new MessageParam { Role = Role.User, Content = prompt }]
        });

        var text = "";
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                text = textBlock.Text;
        }

        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            text = text[jsonStart..(jsonEnd + 1)];

        return JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? [];
    }
}

public class TagAnalysisReport
{
    public int TotalEntries { get; set; }
    public int TaggedEntries { get; set; }
    public int UntaggedEntries { get; set; }
    public int UniqueTagCount { get; set; }
    public List<TagFrequency> TagFrequencies { get; set; } = [];
}

public record TagFrequency(string Tag, int Count);
