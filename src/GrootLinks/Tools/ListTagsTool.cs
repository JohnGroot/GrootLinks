using System.ComponentModel;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class ListTagsTool
{
    [McpServerTool(Name = "list_tags", ReadOnly = true)]
    [Description("List available tags from the taxonomy. Optionally filter by top-level category.")]
    public string ListTags(
        TaxonomyService taxonomy,
        [Description("Optional top-level category to filter (e.g., 'technology', 'culture')")] string? category = null)
    {
        if (category != null)
            return taxonomy.GetTaxonomyTreeJson(category);

        return taxonomy.GetTaxonomyTreeJson();
    }
}
