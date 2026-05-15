using FluentAssertions;
using GrootLinks.Services;

namespace GrootLinks.Tests.Services;

public class LinkParserTests
{
    [Fact]
    public void ExtractFromHtml_ParsesTitleAndDescription()
    {
        var html = """
        <html>
        <head>
            <title>Test Page Title</title>
            <meta name="description" content="A test description of the page.">
        </head>
        <body>
            <h1>Main Heading</h1>
            <p>First paragraph of content.</p>
            <p>Second paragraph with more details.</p>
        </body>
        </html>
        """;

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("Test Page Title");
        result.Description.Should().Be("A test description of the page.");
        result.BodyText.Should().Contain("First paragraph");
    }

    [Fact]
    public void ExtractFromHtml_FallsBackToH1WhenNoTitle()
    {
        var html = "<html><body><h1>Heading As Title</h1><p>Content.</p></body></html>";

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("Heading As Title");
    }

    [Fact]
    public void ExtractFromHtml_TruncatesLongBody()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 1000));
        var html = $"<html><head><title>Long</title></head><body><p>{longText}</p></body></html>";

        var result = LinkParser.ExtractFromHtml(html);

        result.BodyText.Length.Should().BeLessThanOrEqualTo(2500);
    }

    [Fact]
    public void ExtractFromHtml_HandlesOgTags()
    {
        var html = """
        <html>
        <head>
            <meta property="og:title" content="OG Title">
            <meta property="og:description" content="OG Description">
        </head>
        <body><p>Body text.</p></body>
        </html>
        """;

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("OG Title");
        result.Description.Should().Be("OG Description");
    }

    [Fact]
    public void ExtractFromHtml_ReturnsUntitledForEmpty()
    {
        var html = "<html><body></body></html>";

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("Untitled");
    }
}
