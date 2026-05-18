using FluentAssertions;
using GrootLinks.Services;

namespace GrootLinks.Tests.Services;

public class TagClassifierTests
{
    [Fact]
    public void BuildClassificationPrompt_IncludesTaxonomyAndContent()
    {
        var prompt = TagClassifier.BuildClassificationPrompt(
            title: "Introduction to Rust Programming",
            description: "A beginner's guide to Rust",
            bodyText: "Rust is a systems programming language focused on safety...",
            taxonomyJson: """{ "technology": { "programming": { "rust": {} } } }"""
        );

        prompt.Should().Contain("Introduction to Rust Programming");
        prompt.Should().Contain("beginner's guide");
        prompt.Should().Contain("taxonomy");
        prompt.Should().Contain("rust");
    }

    [Fact]
    public void ParseClassificationResponse_ExtractsTagList()
    {
        var tags = TagClassifier.ParseClassificationResponse("""["programming", "rust", "tutorial"]""");

        tags.Should().BeEquivalentTo(["programming", "rust", "tutorial"]);
    }

    [Fact]
    public void ParseClassificationResponse_HandlesMarkdownCodeBlock()
    {
        var response = """
        Here are the tags:
        ```json
        ["ai", "machine-learning"]
        ```
        """;

        var tags = TagClassifier.ParseClassificationResponse(response);

        tags.Should().BeEquivalentTo(["ai", "machine-learning"]);
    }

    [Fact]
    public void ParseClassificationResponse_ReturnsEmptyOnGarbage()
    {
        var tags = TagClassifier.ParseClassificationResponse("I don't know what tags to assign.");

        tags.Should().BeEmpty();
    }

    [Fact]
    public void ParseClassificationResponse_HandlesSingleElementArray()
    {
        var tags = TagClassifier.ParseClassificationResponse("""["ai"]""");

        tags.Should().BeEquivalentTo(["ai"]);
    }
}
