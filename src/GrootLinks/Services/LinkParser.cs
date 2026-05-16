using System.Net;
using HtmlAgilityPack;

namespace GrootLinks.Services;

public record ParsedPage(string Title, string? Description, string BodyText);

public class LinkParser
{
    private readonly HttpClient _httpClient;

    public LinkParser(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ParsedPage> FetchAndParseAsync(string url)
    {
        var response = await _httpClient.GetStringAsync(url);
        return ExtractFromHtml(response);
    }

    public static ParsedPage ExtractFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractTitle(doc);
        var description = ExtractDescription(doc);
        var bodyText = ExtractBodyText(doc);

        return new ParsedPage(title, description, bodyText);
    }

    private static string ExtractTitle(HtmlDocument doc)
    {
        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")
            ?.GetAttributeValue("content", "");
        if (!string.IsNullOrWhiteSpace(ogTitle)) return WebUtility.HtmlDecode(ogTitle);

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(title)) return WebUtility.HtmlDecode(title);

        var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(h1)) return WebUtility.HtmlDecode(h1);

        return "Untitled";
    }

    private static string? ExtractDescription(HtmlDocument doc)
    {
        var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")
            ?.GetAttributeValue("content", "");
        if (!string.IsNullOrWhiteSpace(ogDesc)) return WebUtility.HtmlDecode(ogDesc);

        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")
            ?.GetAttributeValue("content", "");
        return string.IsNullOrWhiteSpace(metaDesc) ? null : WebUtility.HtmlDecode(metaDesc);
    }

    private static string ExtractBodyText(HtmlDocument doc)
    {
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer");
        if (nodesToRemove != null)
            foreach (var node in nodesToRemove.ToList())
                node.Remove();

        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body == null) return "";

        var text = WebUtility.HtmlDecode(body.InnerText);
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        var joined = string.Join(" ", lines);

        if (joined.Length > 2500)
            joined = joined[..2500];

        return joined;
    }
}
