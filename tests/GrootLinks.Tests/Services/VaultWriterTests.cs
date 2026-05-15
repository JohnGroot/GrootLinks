using FluentAssertions;
using GrootLinks.Models;
using GrootLinks.Services;

namespace GrootLinks.Tests.Services;

public class VaultWriterTests : IDisposable
{
    private readonly string _vaultDir;

    public VaultWriterTests()
    {
        _vaultDir = Path.Combine(Path.GetTempPath(), $"grootlinks-vault-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_vaultDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_vaultDir))
            Directory.Delete(_vaultDir, true);
    }

    [Fact]
    public async Task WriteLink_CreatesMarkdownFileWithFrontmatter()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "Test Link",
            Url = "https://example.com",
            Tags = ["programming", "dotnet"],
            Created = new DateOnly(2025, 3, 15),
            Source = "mcp",
            NeedsReview = true,
            Description = "A test description."
        };

        var filePath = await writer.WriteLinkAsync(link);

        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath!);
        content.Should().Contain("title: \"Test Link\"");
        content.Should().Contain("url: https://example.com");
        content.Should().Contain("- programming");
        content.Should().Contain("- dotnet");
        content.Should().Contain("created: 2025-03-15");
        content.Should().Contain("source: mcp");
        content.Should().Contain("needs_review: true");
        content.Should().Contain("# Test Link");
        content.Should().Contain("A test description.");
    }

    [Fact]
    public async Task WriteLink_CreatesYearSubdirectory()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "Year Test",
            Url = "https://example.com/year",
            Created = new DateOnly(2023, 6, 1)
        };

        var filePath = await writer.WriteLinkAsync(link);

        filePath.Should().Contain(Path.Combine("links", "2023"));
    }

    [Fact]
    public async Task WriteLink_SlugifiesTitle()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "Rive — a new way to design & build UIs!",
            Url = "https://rive.app"
        };

        var filePath = await writer.WriteLinkAsync(link);

        Path.GetFileName(filePath!).Should().Be("rive-a-new-way-to-design-build-uis.md");
    }

    [Fact]
    public async Task WriteLink_HandlesEmptyTags()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "No Tags",
            Url = "https://example.com/notags",
            Tags = []
        };

        var filePath = await writer.WriteLinkAsync(link);

        var content = await File.ReadAllTextAsync(filePath!);
        content.Should().Contain("tags: []");
    }

    [Fact]
    public async Task WriteLink_SkipsDuplicateUrl()
    {
        var writer = new VaultWriter(_vaultDir);
        var link1 = new Link { Title = "First", Url = "https://example.com/dup" };
        var link2 = new Link { Title = "Second", Url = "https://example.com/dup" };

        await writer.WriteLinkAsync(link1);
        var path2 = await writer.WriteLinkAsync(link2);

        path2.Should().BeNull("duplicate URL should be skipped");
    }

    [Fact]
    public async Task WriteLink_HandlesTitleCollision()
    {
        var writer = new VaultWriter(_vaultDir);
        var link1 = new Link { Title = "Same Title", Url = "https://a.example.com" };
        var link2 = new Link { Title = "Same Title", Url = "https://b.example.com" };

        var path1 = await writer.WriteLinkAsync(link1);
        var path2 = await writer.WriteLinkAsync(link2);

        path1.Should().NotBe(path2);
        Path.GetFileName(path2!).Should().Be("same-title-2.md");
    }

    [Fact]
    public async Task ReadLink_ParsesFrontmatter()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "Roundtrip Test",
            Url = "https://example.com/roundtrip",
            Tags = ["ai", "programming"],
            Created = new DateOnly(2024, 1, 10),
            Source = "notion",
            NeedsReview = false,
            Description = "Roundtrip desc."
        };

        var filePath = await writer.WriteLinkAsync(link);
        var read = await writer.ReadLinkAsync(filePath!);

        read.Should().NotBeNull();
        read!.Title.Should().Be("Roundtrip Test");
        read.Url.Should().Be("https://example.com/roundtrip");
        read.Tags.Should().BeEquivalentTo(["ai", "programming"]);
        read.NeedsReview.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateLinkTags_ModifiesFrontmatter()
    {
        var writer = new VaultWriter(_vaultDir);
        var link = new Link
        {
            Title = "Update Test",
            Url = "https://example.com/update",
            Tags = ["old-tag"],
            NeedsReview = true
        };

        var filePath = await writer.WriteLinkAsync(link);
        await writer.UpdateLinkTagsAsync(filePath!, ["new-tag-1", "new-tag-2"], clearReview: true);

        var updated = await writer.ReadLinkAsync(filePath!);
        updated!.Tags.Should().BeEquivalentTo(["new-tag-1", "new-tag-2"]);
        updated.NeedsReview.Should().BeFalse();
    }

    [Fact]
    public async Task SearchLinks_FindsByTextAndTags()
    {
        var writer = new VaultWriter(_vaultDir);
        await writer.WriteLinkAsync(new Link { Title = "Rust Programming Guide", Url = "https://rust.example.com", Tags = ["programming", "rust"] });
        await writer.WriteLinkAsync(new Link { Title = "AI News Today", Url = "https://ai.example.com", Tags = ["ai"] });
        await writer.WriteLinkAsync(new Link { Title = "Rust Compiler Internals", Url = "https://rust2.example.com", Tags = ["programming", "rust"] });

        var byText = await writer.SearchLinksAsync("Rust");
        byText.Should().HaveCount(2);

        var byTag = await writer.SearchLinksAsync(tags: ["ai"]);
        byTag.Should().HaveCount(1);
        byTag[0].Title.Should().Be("AI News Today");
    }

    [Fact]
    public async Task GetReviewQueue_ReturnsOnlyNeedsReview()
    {
        var writer = new VaultWriter(_vaultDir);
        await writer.WriteLinkAsync(new Link { Title = "Reviewed", Url = "https://r.example.com", NeedsReview = false });
        await writer.WriteLinkAsync(new Link { Title = "Pending", Url = "https://p.example.com", NeedsReview = true });

        var queue = await writer.GetReviewQueueAsync();
        queue.Should().HaveCount(1);
        queue[0].Title.Should().Be("Pending");
    }
}
