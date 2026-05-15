using System.Text.Json;

namespace GrootLinks.Services;

public class TaxonomyService
{
    private readonly Dictionary<string, JsonElement> _taxonomy;
    private readonly Dictionary<string, string> _aliases;
    private readonly HashSet<string> _allSlugs;
    private readonly string _tagsPath;

    public TaxonomyService(string tagsPath, string aliasesPath)
    {
        _tagsPath = tagsPath;
        var tagsJson = File.ReadAllText(tagsPath);
        _taxonomy = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tagsJson)!;

        var aliasJson = File.ReadAllText(aliasesPath);
        _aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(aliasJson)!;

        _allSlugs = [];
        CollectSlugs(_taxonomy, _allSlugs);
    }

    public List<string> GetAllTagSlugs() => [.. _allSlugs];

    public bool IsValidTag(string tag) => _allSlugs.Contains(tag);

    public string ResolveAlias(string tag)
    {
        if (_aliases.TryGetValue(tag, out var resolved))
            return resolved;
        if (_allSlugs.Contains(tag))
            return tag;
        return tag;
    }

    public List<string> ResolveAliases(IEnumerable<string> tags)
    {
        return tags.Select(ResolveAlias).Distinct().ToList();
    }

    public string GetTaxonomyTreeJson()
    {
        return File.ReadAllText(_tagsPath);
    }

    public string GetTaxonomyTreeJson(string category)
    {
        if (_taxonomy.TryGetValue(category, out var node))
            return node.GetRawText();
        return "{}";
    }

    private static void CollectSlugs(Dictionary<string, JsonElement> node, HashSet<string> slugs)
    {
        foreach (var (key, value) in node)
        {
            if (key.StartsWith('_')) continue;
            slugs.Add(key);
            if (value.ValueKind == JsonValueKind.Object)
            {
                var children = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value.GetRawText());
                if (children != null)
                    CollectSlugs(children, slugs);
            }
        }
    }
}
