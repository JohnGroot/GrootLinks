using FluentAssertions;
using GrootLinks.Services;

namespace GrootLinks.Tests.Services;

public class TaxonomyServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tagsPath;
    private readonly string _aliasesPath;

    public TaxonomyServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"grootlinks-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tagsPath = Path.Combine(_tempDir, "tags.json");
        _aliasesPath = Path.Combine(_tempDir, "tag_aliases.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void LoadTaxonomy_ParsesNestedStructure()
    {
        File.WriteAllText(_tagsPath, """
        {
          "technology": {
            "_description": "Tech stuff",
            "programming": { "dotnet": {}, "rust": {} },
            "ai": {}
          }
        }
        """);
        File.WriteAllText(_aliasesPath, "{}");

        var service = new TaxonomyService(_tagsPath, _aliasesPath);

        service.GetAllTagSlugs().Should().Contain(["technology", "programming", "dotnet", "rust", "ai"]);
    }

    [Fact]
    public void IsValidTag_ReturnsTrueForKnownTags()
    {
        File.WriteAllText(_tagsPath, """
        { "technology": { "programming": { "dotnet": {} } } }
        """);
        File.WriteAllText(_aliasesPath, "{}");

        var service = new TaxonomyService(_tagsPath, _aliasesPath);

        service.IsValidTag("dotnet").Should().BeTrue();
        service.IsValidTag("programming").Should().BeTrue();
        service.IsValidTag("unknown-tag").Should().BeFalse();
    }

    [Fact]
    public void ResolveAlias_MapsOldTagToNew()
    {
        File.WriteAllText(_tagsPath, """
        { "technology": { "ai": {} } }
        """);
        File.WriteAllText(_aliasesPath, """
        { "AI": "ai", "Artificial Intelligence": "ai" }
        """);

        var service = new TaxonomyService(_tagsPath, _aliasesPath);

        service.ResolveAlias("AI").Should().Be("ai");
        service.ResolveAlias("Artificial Intelligence").Should().Be("ai");
        service.ResolveAlias("ai").Should().Be("ai");
    }

    [Fact]
    public void ResolveAliases_MapsListDeduplicates()
    {
        File.WriteAllText(_tagsPath, """
        { "technology": { "ai": {}, "programming": {} } }
        """);
        File.WriteAllText(_aliasesPath, """
        { "AI": "ai", "Computer": "programming" }
        """);

        var service = new TaxonomyService(_tagsPath, _aliasesPath);

        var resolved = service.ResolveAliases(["AI", "Computer", "programming"]);
        resolved.Should().BeEquivalentTo(["ai", "programming"]);
    }

    [Fact]
    public void GetTaxonomyTree_ReturnsJsonString()
    {
        File.WriteAllText(_tagsPath, """
        { "technology": { "ai": {} } }
        """);
        File.WriteAllText(_aliasesPath, "{}");

        var service = new TaxonomyService(_tagsPath, _aliasesPath);

        var tree = service.GetTaxonomyTreeJson();
        tree.Should().Contain("technology");
        tree.Should().Contain("ai");
    }
}
