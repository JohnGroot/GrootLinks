using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Messages;

namespace GrootLinks.Services;

public partial class TagClassifier
{
    private readonly AnthropicClient _client;
    private readonly TaxonomyService _taxonomy;

    public TagClassifier(AnthropicClient client, TaxonomyService taxonomy)
    {
        _client = client;
        _taxonomy = taxonomy;
    }

    public async Task<List<string>> ClassifyAsync(string title, string? description, string bodyText)
    {
        var taxonomyJson = _taxonomy.GetTaxonomyTreeJson();
        var prompt = BuildClassificationPrompt(title, description, bodyText, taxonomyJson);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 256,
            Messages = [new MessageParam { Role = Role.User, Content = prompt }]
        });

        var text = "";
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                text = textBlock.Text;
        }

        var tags = ParseClassificationResponse(text);
        return tags.Where(t => _taxonomy.IsValidTag(t)).ToList();
    }

    public static string BuildClassificationPrompt(string title, string? description, string bodyText, string taxonomyJson)
    {
        return $"""
            Classify this web page into 2-5 tags from the taxonomy below.
            Prefer specific leaf tags over broad parent categories.
            If the content spans multiple categories, tag across categories.
            Return ONLY a JSON array of tag slugs. No explanation.

            ## Taxonomy
            {taxonomyJson}

            ## Page
            Title: {title}
            Description: {description ?? "N/A"}
            Content: {bodyText[..Math.Min(bodyText.Length, 1500)]}
            """;
    }

    public static List<string> ParseClassificationResponse(string response)
    {
        var jsonMatch = JsonArrayRegex().Match(response);
        if (!jsonMatch.Success) return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(jsonMatch.Value);
            return parsed ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"\[(?:\s*""[^""]*""\s*,?\s*)*\]", RegexOptions.Singleline)]
    private static partial Regex JsonArrayRegex();
}
