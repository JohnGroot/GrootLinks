# GrootLinks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 1,321 links from Notion to an Obsidian vault with a clean hierarchical tag taxonomy, and build a .NET 10 MCP stdio server for ongoing AI-classified link ingestion.

**Architecture:** A single .NET 10 console app serves as an MCP server over stdio. The Obsidian vault is a flat directory of markdown files with YAML frontmatter, organized by year. Tag classification uses the Claude API against a hierarchical taxonomy tree stored in `tags.json`.

**Tech Stack:** .NET 10, ModelContextProtocol NuGet (v1.3.0+), Anthropic SDK, HtmlAgilityPack, YamlDotNet, System.Text.Json

**Design Spec:** `docs/superpowers/specs/2026-05-15-grootlinks-design.md`

**Notion Database ID:** `79b34536-f152-4abb-a5e6-5ffce622a0bc`
**Notion Token Env Var:** `NOTION_TOKEN` (configured in `.claude/settings.json`)

---

## File Structure

```
~/Dev/GrootLinks/
├── vault/
│   ├── links/                          # One .md file per link, by year
│   ├── _templates/
│   │   └── link.md                     # Obsidian template
│   └── _taxonomy/
│       ├── tags.json                   # Canonical hierarchical taxonomy
│       └── tag_aliases.json            # Old Notion tag → new canonical mapping
├── src/
│   └── GrootLinks/
│       ├── GrootLinks.csproj
│       ├── Program.cs                  # MCP server entry point
│       ├── Tools/
│       │   ├── SaveLinkTool.cs         # save_link MCP tool
│       │   ├── SearchLinksTool.cs      # search_links MCP tool
│       │   ├── ListTagsTool.cs         # list_tags MCP tool
│       │   ├── RetagLinkTool.cs        # retag_link MCP tool
│       │   └── ReviewQueueTool.cs      # review_queue MCP tool
│       ├── Services/
│       │   ├── LinkParser.cs           # Fetch URL, extract title/description
│       │   ├── TagClassifier.cs        # Claude API tag classification
│       │   ├── VaultWriter.cs          # Write/update .md files with frontmatter
│       │   └── TaxonomyService.cs      # Load/query/validate taxonomy tree
│       └── Models/
│           ├── Link.cs                 # Link data model
│           └── Taxonomy.cs             # Taxonomy tree + alias models
├── tests/
│   └── GrootLinks.Tests/
│       ├── GrootLinks.Tests.csproj
│       ├── Services/
│       │   ├── VaultWriterTests.cs
│       │   ├── TaxonomyServiceTests.cs
│       │   ├── LinkParserTests.cs
│       │   └── TagClassifierTests.cs
│       └── Tools/
│           └── SaveLinkToolTests.cs
├── tools/
│   └── migrate/
│       ├── NotionExporter.cs           # One-time: export Notion → JSON
│       ├── TagAnalyzer.cs              # One-time: analyze tags, generate aliases
│       └── VaultMigrator.cs            # One-time: transform + write vault
├── .gitignore
└── GrootLinks.sln
```

---

## Task 1: Project Scaffolding & Git Setup

**Files:**
- Create: `.gitignore`
- Create: `GrootLinks.sln`
- Create: `src/GrootLinks/GrootLinks.csproj`
- Create: `src/GrootLinks/Program.cs`
- Create: `tests/GrootLinks.Tests/GrootLinks.Tests.csproj`

- [ ] **Step 1: Create .gitignore**

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.cache
*.dll
*.exe

# IDE
.vs/
.idea/
*.swp

# Secrets
.env
appsettings.*.json
!appsettings.json

# Notion export cache
tools/migrate/export/

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 2: Create the solution and projects**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet new sln --name GrootLinks
dotnet new console -n GrootLinks -o src/GrootLinks --framework net10.0
dotnet new xunit -n GrootLinks.Tests -o tests/GrootLinks.Tests --framework net10.0
dotnet sln add src/GrootLinks/GrootLinks.csproj
dotnet sln add tests/GrootLinks.Tests/GrootLinks.Tests.csproj
dotnet add tests/GrootLinks.Tests reference src/GrootLinks
```

Expected: Solution with two projects, test project references main project.

- [ ] **Step 3: Add NuGet dependencies to main project**

Run:
```bash
cd ~/Dev/GrootLinks/src/GrootLinks
dotnet add package ModelContextProtocol --prerelease
dotnet add package Anthropic --prerelease
dotnet add package HtmlAgilityPack
dotnet add package YamlDotNet
```

- [ ] **Step 4: Add test dependencies**

Run:
```bash
cd ~/Dev/GrootLinks/tests/GrootLinks.Tests
dotnet add package NSubstitute
dotnet add package FluentAssertions
```

- [ ] **Step 5: Write minimal Program.cs (MCP stdio server skeleton)**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "grootlinks",
            Version = "0.1.0"
        };
        options.ServerInstructions = "GrootLinks: Save, search, and manage a personal links vault with AI-powered tag classification.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();
await host.RunAsync();
```

- [ ] **Step 6: Verify it builds**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add .gitignore GrootLinks.sln src/ tests/
git commit -m "feat: scaffold .NET 10 solution with MCP server skeleton"
```

---

## Task 2: Models & Taxonomy Service

**Files:**
- Create: `src/GrootLinks/Models/Link.cs`
- Create: `src/GrootLinks/Models/Taxonomy.cs`
- Create: `src/GrootLinks/Services/TaxonomyService.cs`
- Create: `tests/GrootLinks.Tests/Services/TaxonomyServiceTests.cs`
- Create: `vault/_taxonomy/tags.json` (starter taxonomy)
- Create: `vault/_taxonomy/tag_aliases.json` (empty starter)

- [ ] **Step 1: Create the Link model**

Create `src/GrootLinks/Models/Link.cs`:

```csharp
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
```

- [ ] **Step 2: Create the Taxonomy model**

Create `src/GrootLinks/Models/Taxonomy.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrootLinks.Models;

public class TaxonomyNode
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Dictionary<string, TaxonomyNode> Children { get; set; } = [];

    public List<string> GetAllTagSlugs()
    {
        var slugs = new List<string> { Name };
        foreach (var child in Children.Values)
            slugs.AddRange(child.GetAllTagSlugs());
        return slugs;
    }
}

public class TagAliases
{
    public Dictionary<string, string> Mappings { get; set; } = [];
}
```

- [ ] **Step 3: Create starter tags.json**

Create `vault/_taxonomy/tags.json` — a minimal starter taxonomy that will be expanded during migration:

```json
{
  "technology": {
    "_description": "Software, hardware, and digital infrastructure",
    "programming": {},
    "ai": {},
    "game-dev": {},
    "dev-tools": {},
    "infrastructure": {},
    "hardware": {},
    "security": {},
    "platforms": {}
  },
  "culture": {
    "_description": "Arts, media, and cultural production",
    "music": {},
    "visual-art": {},
    "film": {},
    "television": {},
    "literature": {},
    "games": {},
    "fashion": {},
    "design": {}
  },
  "politics": {
    "_description": "Political theory, movements, and current events",
    "left-theory": {},
    "movements": {},
    "geopolitics": {},
    "policy": {},
    "policing": {}
  },
  "theory": {
    "_description": "Academic and intellectual frameworks",
    "philosophy": {},
    "economics": {},
    "complexity": {},
    "ecology": {},
    "urbanism": {}
  },
  "lifestyle": {
    "_description": "Food, health, home, and daily life",
    "food": {},
    "health": {},
    "home": {},
    "outdoor": {},
    "travel": {}
  },
  "commerce": {
    "_description": "Shopping, products, and brands",
    "shops": {},
    "gear": {},
    "clothing": {},
    "tech-gear": {}
  },
  "media-format": {
    "_description": "Content format/type tags",
    "podcast": {},
    "video": {},
    "blog": {},
    "tutorial": {},
    "interview": {}
  },
  "internet-culture": {
    "_description": "Online communities, platforms, and digital culture",
    "social-media": {},
    "web3": {},
    "communities": {},
    "memes": {}
  }
}
```

- [ ] **Step 4: Create empty tag_aliases.json**

Create `vault/_taxonomy/tag_aliases.json`:

```json
{}
```

- [ ] **Step 5: Write TaxonomyService tests**

Create `tests/GrootLinks.Tests/Services/TaxonomyServiceTests.cs`:

```csharp
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
    public void ResolveAliases_MapsListOfOldTags()
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
```

- [ ] **Step 6: Run tests to verify they fail**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "TaxonomyServiceTests"
```

Expected: Compilation error — `TaxonomyService` does not exist.

- [ ] **Step 7: Implement TaxonomyService**

Create `src/GrootLinks/Services/TaxonomyService.cs`:

```csharp
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
```

- [ ] **Step 8: Run tests to verify they pass**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "TaxonomyServiceTests"
```

Expected: All 5 tests pass.

- [ ] **Step 9: Commit**

```bash
git add src/GrootLinks/Models/ src/GrootLinks/Services/TaxonomyService.cs tests/GrootLinks.Tests/Services/TaxonomyServiceTests.cs vault/_taxonomy/
git commit -m "feat: add Link/Taxonomy models and TaxonomyService with alias resolution"
```

---

## Task 3: VaultWriter Service

**Files:**
- Create: `src/GrootLinks/Services/VaultWriter.cs`
- Create: `tests/GrootLinks.Tests/Services/VaultWriterTests.cs`
- Create: `vault/_templates/link.md`

- [ ] **Step 1: Create the Obsidian link template**

Create `vault/_templates/link.md`:

```markdown
---
title: "{{title}}"
url: {{url}}
tags: {{tags}}
created: {{created}}
source: {{source}}
needs_review: {{needs_review}}
---

# {{title}}

{{description}}
```

This is for Obsidian's Templater plugin reference — the actual writing is done by VaultWriter in code.

- [ ] **Step 2: Write VaultWriter tests**

Create `tests/GrootLinks.Tests/Services/VaultWriterTests.cs`:

```csharp
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
        var content = await File.ReadAllTextAsync(filePath);
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

        Path.GetFileName(filePath).Should().Be("rive-a-new-way-to-design-build-uis.md");
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

        var content = await File.ReadAllTextAsync(filePath);
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "VaultWriterTests"
```

Expected: Compilation error — `VaultWriter` does not exist.

- [ ] **Step 4: Implement VaultWriter**

Create `src/GrootLinks/Services/VaultWriter.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using GrootLinks.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GrootLinks.Services;

public partial class VaultWriter
{
    private readonly string _vaultDir;
    private readonly string _linksDir;

    public VaultWriter(string vaultDir)
    {
        _vaultDir = vaultDir;
        _linksDir = Path.Combine(vaultDir, "links");
    }

    public async Task<string?> WriteLinkAsync(Link link)
    {
        var existing = FindByUrl(link.Url);
        if (existing != null) return null;

        var yearDir = Path.Combine(_linksDir, link.Created.Year.ToString());
        Directory.CreateDirectory(yearDir);

        var slug = Slugify(link.Title);
        var filePath = Path.Combine(yearDir, $"{slug}.md");

        var content = BuildMarkdown(link);
        await File.WriteAllTextAsync(filePath, content);

        link.FilePath = filePath;
        return filePath;
    }

    public async Task<Link?> ReadLinkAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var content = await File.ReadAllTextAsync(filePath);
        return ParseMarkdown(content, filePath);
    }

    public async Task UpdateLinkTagsAsync(string filePath, List<string> newTags, bool clearReview = false)
    {
        var link = await ReadLinkAsync(filePath);
        if (link == null) return;

        link.Tags = newTags;
        if (clearReview) link.NeedsReview = false;

        var content = BuildMarkdown(link);
        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task<List<Link>> SearchLinksAsync(string? query = null, List<string>? tags = null)
    {
        var results = new List<Link>();
        if (!Directory.Exists(_linksDir)) return results;

        foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
        {
            var link = await ReadLinkAsync(file);
            if (link == null) continue;

            if (query != null && !link.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            if (tags != null && tags.Count > 0 && !tags.Any(t => link.Tags.Contains(t)))
                continue;

            results.Add(link);
        }

        return results;
    }

    public async Task<List<Link>> GetReviewQueueAsync(int limit = 50)
    {
        var results = new List<Link>();
        if (!Directory.Exists(_linksDir)) return results;

        foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
        {
            if (results.Count >= limit) break;
            var link = await ReadLinkAsync(file);
            if (link is { NeedsReview: true })
                results.Add(link);
        }

        return results;
    }

    private string? FindByUrl(string url)
    {
        if (!Directory.Exists(_linksDir)) return null;

        foreach (var file in Directory.EnumerateFiles(_linksDir, "*.md", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            if (content.Contains($"url: {url}"))
                return file;
        }

        return null;
    }

    private static string BuildMarkdown(Link link)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{link.Title.Replace("\"", "\\\"")}\"");
        sb.AppendLine($"url: {link.Url}");
        if (link.Tags.Count > 0)
        {
            sb.AppendLine("tags:");
            foreach (var tag in link.Tags)
                sb.AppendLine($"  - {tag}");
        }
        else
        {
            sb.AppendLine("tags: []");
        }
        sb.AppendLine($"created: {link.Created:yyyy-MM-dd}");
        sb.AppendLine($"source: {link.Source}");
        sb.AppendLine($"needs_review: {(link.NeedsReview ? "true" : "false")}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {link.Title}");
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            sb.AppendLine();
            sb.AppendLine(link.Description);
        }

        return sb.ToString();
    }

    private static Link? ParseMarkdown(string content, string filePath)
    {
        var frontmatterMatch = FrontmatterRegex().Match(content);
        if (!frontmatterMatch.Success) return null;

        var yaml = frontmatterMatch.Groups[1].Value;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var dict = deserializer.Deserialize<Dictionary<string, object?>>(yaml);
        if (dict == null) return null;

        var tags = new List<string>();
        if (dict.TryGetValue("tags", out var tagsObj) && tagsObj is List<object> tagList)
            tags = tagList.Select(t => t.ToString()!).ToList();

        var created = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dict.TryGetValue("created", out var createdObj) && createdObj != null)
        {
            if (createdObj is DateTime dt)
                created = DateOnly.FromDateTime(dt);
            else if (DateOnly.TryParse(createdObj.ToString(), out var parsed))
                created = parsed;
        }

        return new Link
        {
            Title = dict.GetValueOrDefault("title")?.ToString() ?? "",
            Url = dict.GetValueOrDefault("url")?.ToString() ?? "",
            Tags = tags,
            Created = created,
            Source = dict.GetValueOrDefault("source")?.ToString() ?? "unknown",
            NeedsReview = dict.GetValueOrDefault("needs_review")?.ToString() == "True",
            Description = ExtractDescription(content),
            FilePath = filePath
        };
    }

    private static string? ExtractDescription(string content)
    {
        var afterFrontmatter = FrontmatterRegex().Replace(content, "").Trim();
        var lines = afterFrontmatter.Split('\n').Select(l => l.Trim()).Where(l => !l.StartsWith('#') && l.Length > 0).ToList();
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    internal static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = SlugStripRegex().Replace(slug, "");
        slug = SlugWhitespaceRegex().Replace(slug, "-");
        slug = SlugMultiDashRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        return slug;
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"[^\w\s-]")]
    private static partial Regex SlugStripRegex();

    [GeneratedRegex(@"[\s]+")]
    private static partial Regex SlugWhitespaceRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex SlugMultiDashRegex();
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "VaultWriterTests"
```

Expected: All 9 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/GrootLinks/Services/VaultWriter.cs tests/GrootLinks.Tests/Services/VaultWriterTests.cs vault/_templates/
git commit -m "feat: add VaultWriter with markdown frontmatter read/write/search"
```

---

## Task 4: LinkParser Service

**Files:**
- Create: `src/GrootLinks/Services/LinkParser.cs`
- Create: `tests/GrootLinks.Tests/Services/LinkParserTests.cs`

- [ ] **Step 1: Write LinkParser tests**

Create `tests/GrootLinks.Tests/Services/LinkParserTests.cs`:

```csharp
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
            <p>First paragraph of content that provides context about the page.</p>
            <p>Second paragraph with more details.</p>
        </body>
        </html>
        """;

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("Test Page Title");
        result.Description.Should().Be("A test description of the page.");
        result.BodyText.Should().Contain("First paragraph");
        result.BodyText.Should().Contain("Second paragraph");
    }

    [Fact]
    public void ExtractFromHtml_FallsBackToH1WhenNoTitle()
    {
        var html = """
        <html><body><h1>Heading As Title</h1><p>Content.</p></body></html>
        """;

        var result = LinkParser.ExtractFromHtml(html);

        result.Title.Should().Be("Heading As Title");
    }

    [Fact]
    public void ExtractFromHtml_TruncatesLongBody()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 1000));
        var html = $"<html><head><title>Long</title></head><body><p>{longText}</p></body></html>";

        var result = LinkParser.ExtractFromHtml(html);

        result.BodyText.Length.Should().BeLessOrEqualTo(2500);
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "LinkParserTests"
```

Expected: Compilation error — `LinkParser` does not exist.

- [ ] **Step 3: Implement LinkParser**

Create `src/GrootLinks/Services/LinkParser.cs`:

```csharp
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
            ?.GetAttributeValue("content", null);
        if (!string.IsNullOrWhiteSpace(ogTitle)) return ogTitle;

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(title)) return WebUtility.HtmlDecode(title);

        var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(h1)) return WebUtility.HtmlDecode(h1);

        return "Untitled";
    }

    private static string? ExtractDescription(HtmlDocument doc)
    {
        var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")
            ?.GetAttributeValue("content", null);
        if (!string.IsNullOrWhiteSpace(ogDesc)) return ogDesc;

        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")
            ?.GetAttributeValue("content", null);
        return string.IsNullOrWhiteSpace(metaDesc) ? null : metaDesc;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "LinkParserTests"
```

Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/GrootLinks/Services/LinkParser.cs tests/GrootLinks.Tests/Services/LinkParserTests.cs
git commit -m "feat: add LinkParser for URL content extraction"
```

---

## Task 5: TagClassifier Service

**Files:**
- Create: `src/GrootLinks/Services/TagClassifier.cs`
- Create: `tests/GrootLinks.Tests/Services/TagClassifierTests.cs`

- [ ] **Step 1: Write TagClassifier tests**

Create `tests/GrootLinks.Tests/Services/TagClassifierTests.cs`:

```csharp
using FluentAssertions;
using GrootLinks.Services;
using NSubstitute;

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
        var response = """["programming", "rust", "tutorial"]""";

        var tags = TagClassifier.ParseClassificationResponse(response);

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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "TagClassifierTests"
```

Expected: Compilation error — `TagClassifier` does not exist.

- [ ] **Step 3: Implement TagClassifier**

Create `src/GrootLinks/Services/TagClassifier.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;

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

        var response = await _client.Messages.CreateAsync(new()
        {
            Model = "claude-sonnet-4-5-20250514",
            MaxTokens = 256,
            Messages = [new() { Role = "user", Content = prompt }]
        });

        var text = response.Content[0].Text ?? "";
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

    [GeneratedRegex(@"\[""[^\]]*""\]", RegexOptions.Singleline)]
    private static partial Regex JsonArrayRegex();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --filter "TagClassifierTests"
```

Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/GrootLinks/Services/TagClassifier.cs tests/GrootLinks.Tests/Services/TagClassifierTests.cs
git commit -m "feat: add TagClassifier with Claude API integration for tag classification"
```

---

## Task 6: MCP Tools

**Files:**
- Create: `src/GrootLinks/Tools/SaveLinkTool.cs`
- Create: `src/GrootLinks/Tools/SearchLinksTool.cs`
- Create: `src/GrootLinks/Tools/ListTagsTool.cs`
- Create: `src/GrootLinks/Tools/RetagLinkTool.cs`
- Create: `src/GrootLinks/Tools/ReviewQueueTool.cs`
- Modify: `src/GrootLinks/Program.cs`

- [ ] **Step 1: Implement SaveLinkTool**

Create `src/GrootLinks/Tools/SaveLinkTool.cs`:

```csharp
using System.ComponentModel;
using GrootLinks.Models;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class SaveLinkTool
{
    [McpServerTool(Name = "save_link", ReadOnly = false)]
    [Description("Save a URL to the links vault with AI-powered tag classification. The link is fetched, classified, and saved as an Obsidian markdown file with needs_review: true.")]
    public async Task<string> SaveLink(
        LinkParser parser,
        TagClassifier classifier,
        VaultWriter writer,
        [Description("The URL to save")] string url,
        [Description("Optional manual tags to include (in addition to AI-suggested tags)")] string[]? tags = null,
        [Description("Optional title override (otherwise extracted from page)")] string? title = null)
    {
        var page = await parser.FetchAndParseAsync(url);
        var aiTags = await classifier.ClassifyAsync(page.Title, page.Description, page.BodyText);

        var allTags = new HashSet<string>(aiTags);
        if (tags != null)
            foreach (var t in tags)
                allTags.Add(t);

        var link = new Link
        {
            Title = title ?? page.Title,
            Url = url,
            Tags = [.. allTags],
            Source = "mcp",
            NeedsReview = true,
            Description = page.Description
        };

        var filePath = await writer.WriteLinkAsync(link);
        if (filePath == null)
            return $"Link already exists in vault for URL: {url}";

        return $"Saved: {filePath}\nTags: {string.Join(", ", link.Tags)}\nStatus: needs_review";
    }
}
```

- [ ] **Step 2: Implement SearchLinksTool**

Create `src/GrootLinks/Tools/SearchLinksTool.cs`:

```csharp
using System.ComponentModel;
using System.Text;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class SearchLinksTool
{
    [McpServerTool(Name = "search_links", ReadOnly = true)]
    [Description("Search saved links by text query and/or tags.")]
    public async Task<string> SearchLinks(
        VaultWriter writer,
        [Description("Text to search for in link titles")] string? query = null,
        [Description("Filter by these tags (returns links matching ANY tag)")] string[]? tags = null)
    {
        var results = await writer.SearchLinksAsync(query, tags?.ToList());

        if (results.Count == 0)
            return "No links found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} link(s):");
        foreach (var link in results)
        {
            sb.AppendLine($"- **{link.Title}**");
            sb.AppendLine($"  URL: {link.Url}");
            sb.AppendLine($"  Tags: {string.Join(", ", link.Tags)}");
            sb.AppendLine($"  Review: {(link.NeedsReview ? "pending" : "done")}");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 3: Implement ListTagsTool**

Create `src/GrootLinks/Tools/ListTagsTool.cs`:

```csharp
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
```

- [ ] **Step 4: Implement RetagLinkTool**

Create `src/GrootLinks/Tools/RetagLinkTool.cs`:

```csharp
using System.ComponentModel;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class RetagLinkTool
{
    [McpServerTool(Name = "retag_link", ReadOnly = false)]
    [Description("Update tags on an existing link. Optionally clear the needs_review flag.")]
    public async Task<string> RetagLink(
        VaultWriter writer,
        TaxonomyService taxonomy,
        [Description("Path to the link markdown file")] string filePath,
        [Description("New tags to assign")] string[] tags,
        [Description("Set to true to mark the link as reviewed")] bool clearReview = false)
    {
        var invalid = tags.Where(t => !taxonomy.IsValidTag(t)).ToArray();
        if (invalid.Length > 0)
            return $"Invalid tags not in taxonomy: {string.Join(", ", invalid)}";

        await writer.UpdateLinkTagsAsync(filePath, [.. tags], clearReview);

        var link = await writer.ReadLinkAsync(filePath);
        return $"Updated: {filePath}\nTags: {string.Join(", ", link?.Tags ?? [])}\nReview: {(link?.NeedsReview == true ? "pending" : "cleared")}";
    }
}
```

- [ ] **Step 5: Implement ReviewQueueTool**

Create `src/GrootLinks/Tools/ReviewQueueTool.cs`:

```csharp
using System.ComponentModel;
using System.Text;
using GrootLinks.Services;
using ModelContextProtocol.Server;

namespace GrootLinks.Tools;

[McpServerToolType]
public class ReviewQueueTool
{
    [McpServerTool(Name = "review_queue", ReadOnly = true)]
    [Description("List links that need tag review (needs_review: true).")]
    public async Task<string> ReviewQueue(
        VaultWriter writer,
        [Description("Maximum number of links to return")] int limit = 20)
    {
        var queue = await writer.GetReviewQueueAsync(limit);

        if (queue.Count == 0)
            return "No links pending review.";

        var sb = new StringBuilder();
        sb.AppendLine($"{queue.Count} link(s) pending review:");
        foreach (var link in queue)
        {
            sb.AppendLine($"- **{link.Title}**");
            sb.AppendLine($"  File: {link.FilePath}");
            sb.AppendLine($"  Tags: {string.Join(", ", link.Tags)}");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 6: Update Program.cs with DI registration**

Replace `src/GrootLinks/Program.cs` with:

```csharp
using Anthropic;
using GrootLinks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

var vaultPath = Environment.GetEnvironmentVariable("GROOTLINKS_VAULT_PATH")
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "vault"));

var taxonomyPath = Path.Combine(vaultPath, "_taxonomy", "tags.json");
var aliasesPath = Path.Combine(vaultPath, "_taxonomy", "tag_aliases.json");

builder.Services.AddHttpClient();
builder.Services.AddSingleton(new TaxonomyService(taxonomyPath, aliasesPath));
builder.Services.AddSingleton(new VaultWriter(vaultPath));
builder.Services.AddSingleton<LinkParser>();
builder.Services.AddSingleton(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is required");
    return new AnthropicClient(apiKey);
});
builder.Services.AddSingleton<TagClassifier>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "grootlinks",
            Version = "0.1.0"
        };
        options.ServerInstructions = "GrootLinks: Save, search, and manage a personal links vault with AI-powered tag classification.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();
await host.RunAsync();
```

- [ ] **Step 7: Verify it builds**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/GrootLinks/Tools/ src/GrootLinks/Program.cs
git commit -m "feat: add MCP tools (save_link, search_links, list_tags, retag_link, review_queue)"
```

---

## Task 7: Notion Export & Tag Analysis (Migration Phase 1)

**Files:**
- Create: `tools/migrate/NotionExporter.cs`
- Create: `tools/migrate/TagAnalyzer.cs`

This is a set of one-time scripts run as top-level statements or a simple console project. For simplicity, we'll add them as additional entry points invoked via `dotnet run` with args.

- [ ] **Step 1: Create the migration tools directory and project**

Run:
```bash
mkdir -p ~/Dev/GrootLinks/tools/migrate
cd ~/Dev/GrootLinks
dotnet new console -n GrootLinks.Migrate -o tools/migrate --framework net10.0
dotnet sln add tools/migrate/GrootLinks.Migrate.csproj
dotnet add tools/migrate reference src/GrootLinks
cd ~/Dev/GrootLinks/tools/migrate
dotnet add package System.Text.Json
```

- [ ] **Step 2: Implement NotionExporter**

Create `tools/migrate/NotionExporter.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrootLinks.Migrate;

public class NotionExporter
{
    private readonly HttpClient _http;
    private readonly string _databaseId;

    public NotionExporter(string notionToken, string databaseId)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", notionToken);
        _http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
        _databaseId = databaseId;
    }

    public async Task<List<NotionEntry>> ExportAllAsync()
    {
        var entries = new List<NotionEntry>();
        string? cursor = null;

        while (true)
        {
            var body = new Dictionary<string, object> { ["page_size"] = 100 };
            if (cursor != null) body["start_cursor"] = cursor;

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.notion.com/v1/databases/{_databaseId}/query")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                var props = result.GetProperty("properties");
                var entry = new NotionEntry
                {
                    Id = result.GetProperty("id").GetString()!,
                    Title = ExtractTitle(props),
                    Url = ExtractUrl(props),
                    Tags = ExtractTags(props),
                    Created = ExtractCreated(props)
                };
                entries.Add(entry);
            }

            if (!root.GetProperty("has_more").GetBoolean()) break;
            cursor = root.GetProperty("next_cursor").GetString();
        }

        return entries;
    }

    public static async Task SaveToFileAsync(List<NotionEntry> entries, string path)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static string ExtractTitle(JsonElement props)
    {
        if (props.TryGetProperty("Name", out var name) &&
            name.GetProperty("title").GetArrayLength() > 0)
        {
            return name.GetProperty("title")[0].GetProperty("plain_text").GetString() ?? "";
        }
        return "";
    }

    private static string? ExtractUrl(JsonElement props)
    {
        if (props.TryGetProperty("URL", out var url) &&
            url.GetProperty("url").ValueKind != JsonValueKind.Null)
        {
            return url.GetProperty("url").GetString();
        }
        return null;
    }

    private static List<string> ExtractTags(JsonElement props)
    {
        var tags = new List<string>();
        if (props.TryGetProperty("Tags", out var tagsEl) &&
            tagsEl.GetProperty("multi_select").ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.GetProperty("multi_select").EnumerateArray())
                tags.Add(tag.GetProperty("name").GetString()!);
        }
        return tags;
    }

    private static DateOnly ExtractCreated(JsonElement props)
    {
        if (props.TryGetProperty("Created", out var created) &&
            created.GetProperty("created_time").ValueKind != JsonValueKind.Null)
        {
            var dt = DateTime.Parse(created.GetProperty("created_time").GetString()!);
            return DateOnly.FromDateTime(dt);
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}

public class NotionEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateOnly Created { get; set; }
}
```

- [ ] **Step 3: Implement TagAnalyzer**

Create `tools/migrate/TagAnalyzer.cs`:

```csharp
using System.Text.Json;
using Anthropic;

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

    public static async Task<Dictionary<string, string>> GenerateAliasesAsync(
        TagAnalysisReport report,
        string taxonomyJson,
        AnthropicClient client)
    {
        var tagList = string.Join("\n", report.TagFrequencies.Select(t => $"- {t.Tag} ({t.Count} uses)"));

        var prompt = $"""
            You are mapping old Notion tags to a new canonical taxonomy.
            
            ## New Taxonomy
            {taxonomyJson}
            
            ## Old Tags (with usage frequency)
            {tagList}
            
            For EVERY old tag, provide a mapping to the most appropriate tag slug in the new taxonomy.
            If an old tag doesn't fit any existing taxonomy entry, suggest a new slug that would fit 
            naturally into the taxonomy tree (use kebab-case).
            
            Rules:
            - Fix typos (e.g., "Theroy" -> appropriate tag)
            - Merge duplicates (e.g., "TikTook" and "TikTok" -> same slug)
            - Normalize casing (all kebab-case)
            - Map specific person names to the most relevant topic tag unless they warrant their own tag
            - Prefer specific leaf tags over broad parents
            
            Return ONLY a JSON object mapping old tag -> new slug. No explanation.
            Example: {{"OldTag": "new-slug", "AnotherOld": "another-slug"}}
            """;

        var response = await client.Messages.CreateAsync(new()
        {
            Model = "claude-sonnet-4-5-20250514",
            MaxTokens = 8192,
            Messages = [new() { Role = "user", Content = prompt }]
        });

        var text = response.Content[0].Text ?? "{}";
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
```

- [ ] **Step 4: Implement the migration Program.cs**

Replace `tools/migrate/Program.cs` with:

```csharp
using System.Text.Json;
using Anthropic;
using GrootLinks.Migrate;

var command = args.Length > 0 ? args[0] : "help";
var notionToken = Environment.GetEnvironmentVariable("NOTION_TOKEN")
    ?? throw new InvalidOperationException("NOTION_TOKEN required");
var databaseId = "79b34536-f152-4abb-a5e6-5ffce622a0bc";
var exportDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "export");
Directory.CreateDirectory(exportDir);
var exportPath = Path.Combine(exportDir, "notion_export.json");

switch (command)
{
    case "export":
        Console.WriteLine("Exporting from Notion...");
        var exporter = new NotionExporter(notionToken, databaseId);
        var entries = await exporter.ExportAllAsync();
        await NotionExporter.SaveToFileAsync(entries, exportPath);
        Console.WriteLine($"Exported {entries.Count} entries to {exportPath}");
        break;

    case "analyze":
        Console.WriteLine("Analyzing tags...");
        var data = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;
        var report = TagAnalyzer.Analyze(data);
        Console.WriteLine($"Total: {report.TotalEntries}");
        Console.WriteLine($"Tagged: {report.TaggedEntries}");
        Console.WriteLine($"Untagged: {report.UntaggedEntries}");
        Console.WriteLine($"Unique tags: {report.UniqueTagCount}");
        Console.WriteLine("\nTop 30 tags:");
        foreach (var t in report.TagFrequencies.Take(30))
            Console.WriteLine($"  {t.Tag}: {t.Count}");

        var reportPath = Path.Combine(exportDir, "tag_report.json");
        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(reportPath, reportJson);
        Console.WriteLine($"\nFull report saved to {reportPath}");
        break;

    case "generate-aliases":
        Console.WriteLine("Generating tag aliases via AI...");
        var exportData = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;
        var analysisReport = TagAnalyzer.Analyze(exportData);

        var vaultPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "vault"));
        var taxonomyJson = await File.ReadAllTextAsync(
            Path.Combine(vaultPath, "_taxonomy", "tags.json"));

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY required");
        var anthropic = new AnthropicClient(anthropicKey);

        var aliases = await TagAnalyzer.GenerateAliasesAsync(analysisReport, taxonomyJson, anthropic);

        var aliasPath = Path.Combine(vaultPath, "_taxonomy", "tag_aliases.json");
        var aliasJson = JsonSerializer.Serialize(aliases, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(aliasPath, aliasJson);
        Console.WriteLine($"Generated {aliases.Count} aliases, saved to {aliasPath}");
        Console.WriteLine("\n*** REVIEW tag_aliases.json before running migrate! ***");
        break;

    default:
        Console.WriteLine("Usage: dotnet run -- <command>");
        Console.WriteLine("  export             - Export Notion database to JSON");
        Console.WriteLine("  analyze            - Analyze exported tags");
        Console.WriteLine("  generate-aliases   - AI-generate tag alias mappings");
        Console.WriteLine("  migrate            - Write vault markdown files (Task 8)");
        break;
}
```

- [ ] **Step 5: Verify it builds**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add tools/migrate/
git commit -m "feat: add Notion export and tag analysis migration tools"
```

---

## Task 8: Vault Migration (Migration Phase 2 & 3)

**Files:**
- Create: `tools/migrate/VaultMigrator.cs`
- Modify: `tools/migrate/Program.cs` (add `migrate` command)

- [ ] **Step 1: Implement VaultMigrator**

Create `tools/migrate/VaultMigrator.cs`:

```csharp
using System.Text.Json;
using Anthropic;
using GrootLinks.Models;
using GrootLinks.Services;

namespace GrootLinks.Migrate;

public class VaultMigrator
{
    private readonly VaultWriter _writer;
    private readonly TaxonomyService _taxonomy;
    private readonly TagClassifier? _classifier;

    public VaultMigrator(VaultWriter writer, TaxonomyService taxonomy, TagClassifier? classifier = null)
    {
        _writer = writer;
        _taxonomy = taxonomy;
        _classifier = classifier;
    }

    public async Task<MigrationReport> MigrateAsync(
        List<NotionEntry> entries,
        LinkParser? parser = null,
        Action<string>? log = null)
    {
        var report = new MigrationReport();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                log?.Invoke($"SKIP (no URL): {entry.Title}");
                report.Skipped++;
                continue;
            }

            var resolvedTags = _taxonomy.ResolveAliases(entry.Tags);
            var needsReview = false;

            if (resolvedTags.Count == 0 && _classifier != null && parser != null)
            {
                try
                {
                    log?.Invoke($"AI-CLASSIFY: {entry.Title}");
                    var page = await parser.FetchAndParseAsync(entry.Url);
                    resolvedTags = await _classifier.ClassifyAsync(page.Title, page.Description, page.BodyText);
                    needsReview = true;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"FETCH-FAIL: {entry.Title} - {ex.Message}");
                    needsReview = true;
                }
            }

            if (resolvedTags.Count == 0) needsReview = true;

            var link = new Link
            {
                Title = entry.Title,
                Url = entry.Url,
                Tags = resolvedTags,
                Created = entry.Created,
                Source = "notion",
                NeedsReview = needsReview
            };

            var filePath = await _writer.WriteLinkAsync(link);
            if (filePath == null)
            {
                log?.Invoke($"DUPLICATE: {entry.Title}");
                report.Duplicates++;
                continue;
            }

            log?.Invoke($"OK: {entry.Title} -> {filePath} [{string.Join(", ", resolvedTags)}]");
            report.Migrated++;
            if (needsReview) report.NeedsReview++;
        }

        return report;
    }
}

public class MigrationReport
{
    public int Migrated { get; set; }
    public int Skipped { get; set; }
    public int Duplicates { get; set; }
    public int NeedsReview { get; set; }
}
```

- [ ] **Step 2: Add `migrate` command to Program.cs**

Add this case to the switch statement in `tools/migrate/Program.cs`, before the `default` case:

```csharp
    case "migrate":
        Console.WriteLine("Migrating to vault...");
        var migrateData = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;

        var migrateVaultPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "vault"));
        var migrateTaxPath = Path.Combine(migrateVaultPath, "_taxonomy", "tags.json");
        var migrateAliasPath = Path.Combine(migrateVaultPath, "_taxonomy", "tag_aliases.json");

        var taxonomyService = new TaxonomyService(migrateTaxPath, migrateAliasPath);
        var vaultWriter = new VaultWriter(migrateVaultPath);

        TagClassifier? classifier = null;
        LinkParser? linkParser = null;
        var classifyUntagged = args.Length > 1 && args[1] == "--classify-untagged";

        if (classifyUntagged)
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException("ANTHROPIC_API_KEY required for --classify-untagged");
            var client = new AnthropicClient(apiKey);
            classifier = new TagClassifier(client, taxonomyService);
            var httpClient = new HttpClient();
            linkParser = new LinkParser(httpClient);
            Console.WriteLine("AI classification enabled for untagged entries.");
        }

        var migrator = new VaultMigrator(vaultWriter, taxonomyService, classifier);
        var migrationReport = await migrator.MigrateAsync(
            migrateData, linkParser, msg => Console.WriteLine(msg));

        Console.WriteLine($"\n=== Migration Complete ===");
        Console.WriteLine($"Migrated: {migrationReport.Migrated}");
        Console.WriteLine($"Skipped: {migrationReport.Skipped}");
        Console.WriteLine($"Duplicates: {migrationReport.Duplicates}");
        Console.WriteLine($"Needs Review: {migrationReport.NeedsReview}");
        break;
```

- [ ] **Step 3: Verify it builds**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add tools/migrate/VaultMigrator.cs tools/migrate/Program.cs
git commit -m "feat: add vault migrator with optional AI classification for untagged entries"
```

---

## Task 9: Integration Test & MCP Server Config

**Files:**
- Modify: `.claude/settings.json` (add GrootLinks MCP server)
- Create: `.env.example`

- [ ] **Step 1: Create .env.example**

Create `.env.example`:

```
NOTION_TOKEN=your_notion_integration_token
ANTHROPIC_API_KEY=your_anthropic_api_key
GROOTLINKS_VAULT_PATH=/path/to/vault
```

- [ ] **Step 2: Run the full test suite**

Run:
```bash
cd ~/Dev/GrootLinks
dotnet test --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 3: Build and test the MCP server starts**

Run:
```bash
cd ~/Dev/GrootLinks/src/GrootLinks
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}' | timeout 5 dotnet run 2>/dev/null || true
```

Expected: Server responds with JSON-RPC initialize response (or times out after the init — that's fine, it proves it starts).

- [ ] **Step 4: Add GrootLinks MCP server to Claude Code settings**

Update `.claude/settings.json` to include both Notion and GrootLinks MCP servers:

```json
{
  "mcpServers": {
    "notion": {
      "command": "npx",
      "args": ["-y", "@notionhq/notion-mcp-server"],
      "env": {
        "NOTION_TOKEN": "REDACTED_NOTION_TOKEN"
      }
    },
    "grootlinks": {
      "command": "dotnet",
      "args": ["run", "--project", "src/GrootLinks"],
      "env": {
        "ANTHROPIC_API_KEY": "${ANTHROPIC_API_KEY}",
        "GROOTLINKS_VAULT_PATH": "./vault"
      }
    }
  }
}
```

- [ ] **Step 5: Commit**

```bash
git add .env.example .claude/settings.json
git commit -m "feat: add MCP server config and environment setup"
```

---

## Task 10: Run Migration End-to-End

This task executes the actual migration. Run each step and verify before proceeding.

- [ ] **Step 1: Export Notion database**

Run:
```bash
cd ~/Dev/GrootLinks
NOTION_TOKEN=REDACTED_NOTION_TOKEN dotnet run --project tools/migrate -- export
```

Expected: `Exported 1321 entries to .../notion_export.json`

- [ ] **Step 2: Analyze tags**

Run:
```bash
NOTION_TOKEN=REDACTED_NOTION_TOKEN dotnet run --project tools/migrate -- analyze
```

Expected: Prints tag stats and top 30 tags. Saves full report.

- [ ] **Step 3: Generate AI alias mappings**

Run:
```bash
NOTION_TOKEN=REDACTED_NOTION_TOKEN \
ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
dotnet run --project tools/migrate -- generate-aliases
```

Expected: Generates `vault/_taxonomy/tag_aliases.json` with mappings for all 500+ tags.

- [ ] **Step 4: HUMAN REVIEW GATE — Edit tag_aliases.json**

Open `vault/_taxonomy/tag_aliases.json` and review/edit the AI-generated mappings. Also review and expand `vault/_taxonomy/tags.json` to add any new subcategories suggested by the alias mappings.

This is a critical manual step — do not skip it.

- [ ] **Step 5: Run migration (without AI classification first)**

Run:
```bash
NOTION_TOKEN=REDACTED_NOTION_TOKEN \
dotnet run --project tools/migrate -- migrate
```

Expected: Migrates ~1,182 tagged entries. Reports ~139 needing review (untagged).

- [ ] **Step 6: Run migration with AI classification for untagged**

Run:
```bash
NOTION_TOKEN=REDACTED_NOTION_TOKEN \
ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
dotnet run --project tools/migrate -- migrate --classify-untagged
```

Expected: Processes remaining untagged entries with AI classification. Reports all as needs_review.

- [ ] **Step 7: Verify vault structure**

Run:
```bash
find ~/Dev/GrootLinks/vault/links -type f -name "*.md" | wc -l
ls ~/Dev/GrootLinks/vault/links/
```

Expected: ~1,321 .md files across year directories (2019-2025).

- [ ] **Step 8: Commit the vault**

```bash
cd ~/Dev/GrootLinks
git add vault/
git commit -m "feat: migrate 1,321 links from Notion with cleaned taxonomy"
```

---

## Task 11: Verification & Obsidian Setup

- [ ] **Step 1: Open vault in Obsidian**

Open Obsidian, click "Open folder as vault", select `~/Dev/GrootLinks/vault/`. Verify:
- Links appear in the file explorer under `links/YYYY/`
- Clicking a link shows frontmatter and title
- Tags are visible in the properties view

- [ ] **Step 2: Test MCP server from Claude Code**

Restart Claude Code (to pick up the new MCP config). Then test:
- Ask Claude to `save_link` with a test URL
- Ask Claude to `search_links` for a known term
- Ask Claude to `list_tags` for a category
- Ask Claude to `review_queue` to see pending reviews

- [ ] **Step 3: Verify round-trip**

Save a new link, find it in search, retag it, verify the review flag clears.

- [ ] **Step 4: Final commit**

```bash
cd ~/Dev/GrootLinks
git add -A
git commit -m "chore: finalize GrootLinks setup and verification"
```
