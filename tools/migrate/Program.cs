using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using GrootLinks.Migrate;

var command = args.Length > 0 ? args[0] : "help";
var notionToken = Environment.GetEnvironmentVariable("NOTION_TOKEN")
    ?? throw new InvalidOperationException("NOTION_TOKEN env var required");
var databaseId = Environment.GetEnvironmentVariable("NOTION_DATABASE_ID")
    ?? "79b34536-f152-4abb-a5e6-5ffce622a0bc";
var exportDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "export"));
Directory.CreateDirectory(exportDir);
var exportPath = Path.Combine(exportDir, "notion_export.json");

var vaultPath = Environment.GetEnvironmentVariable("GROOTLINKS_VAULT_PATH")
    ?? throw new InvalidOperationException("GROOTLINKS_VAULT_PATH env var required");

switch (command)
{
    case "export":
    {
        Console.WriteLine("Exporting from Notion...");
        using var exporter = new NotionExporter(notionToken, databaseId);
        var entries = await exporter.ExportAllAsync(Console.WriteLine);
        await NotionExporter.SaveToFileAsync(entries, exportPath);
        Console.WriteLine($"Exported {entries.Count} entries to {exportPath}");
        break;
    }

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
        await File.WriteAllTextAsync(reportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nFull report: {reportPath}");
        break;

    case "suggest-taxonomy":
        Console.WriteLine("Generating taxonomy expansion suggestions...");
        var suggestData = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;
        var suggestReport = TagAnalyzer.Analyze(suggestData);
        var currentTaxonomy = await File.ReadAllTextAsync(
            Path.Combine(vaultPath, "_taxonomy", "tags.json"));
        var suggestClient = new AnthropicClient(new ClientOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException("ANTHROPIC_API_KEY required")
        });
        var expandedTaxonomy = await TagAnalyzer.SuggestTaxonomyExpansionAsync(
            suggestReport, currentTaxonomy, suggestClient);
        var suggestedPath = Path.Combine(exportDir, "suggested_tags.json");
        await File.WriteAllTextAsync(suggestedPath, expandedTaxonomy);
        Console.WriteLine($"Suggested expanded taxonomy saved to: {suggestedPath}");
        Console.WriteLine("\n*** Review suggested_tags.json, then copy approved version to vault/_taxonomy/tags.json ***");
        break;

    case "generate-aliases":
        Console.WriteLine("Generating tag aliases...");
        var aliasData = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;
        var aliasReport = TagAnalyzer.Analyze(aliasData);
        var taxonomyJson = await File.ReadAllTextAsync(
            Path.Combine(vaultPath, "_taxonomy", "tags.json"));
        var aliasClient = new AnthropicClient(new ClientOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException("ANTHROPIC_API_KEY required")
        });
        var aliases = await TagAnalyzer.GenerateAliasesAsync(aliasReport, taxonomyJson, aliasClient);
        var aliasPath = Path.Combine(vaultPath, "_taxonomy", "tag_aliases.json");
        await File.WriteAllTextAsync(aliasPath,
            JsonSerializer.Serialize(aliases, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Generated {aliases.Count} aliases: {aliasPath}");
        Console.WriteLine("\n*** REVIEW tag_aliases.json before running migrate! ***");
        break;

    case "migrate":
        Console.WriteLine("Migrating to vault...");
        var migrateData = JsonSerializer.Deserialize<List<NotionEntry>>(
            await File.ReadAllTextAsync(exportPath))!;

        var migrateTaxPath = Path.Combine(vaultPath, "_taxonomy", "tags.json");
        var migrateAliasPath = Path.Combine(vaultPath, "_taxonomy", "tag_aliases.json");
        var migrationLogPath = Path.Combine(exportDir, "migration_log.json");

        var taxonomyService = new GrootLinks.Services.TaxonomyService(migrateTaxPath, migrateAliasPath);
        var vaultWriter = new GrootLinks.Services.VaultWriter(vaultPath);

        GrootLinks.Services.TagClassifier? classifier = null;
        GrootLinks.Services.LinkParser? linkParser = null;
        AnthropicClient? migrateAnthropicClient = null;
        var classifyUntagged = args.Length > 1 && args[1] == "--classify-untagged";

        if (classifyUntagged)
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException("ANTHROPIC_API_KEY required for --classify-untagged");
            migrateAnthropicClient = new AnthropicClient(new ClientOptions { ApiKey = apiKey });
            classifier = new GrootLinks.Services.TagClassifier(migrateAnthropicClient, taxonomyService);
            linkParser = new GrootLinks.Services.LinkParser(new HttpClient());
            Console.WriteLine("AI classification enabled for untagged entries.");
        }

        var migrator = new VaultMigrator(vaultWriter, taxonomyService, migrationLogPath, classifier);
        var migrationReport = await migrator.MigrateAsync(
            migrateData, linkParser, migrateAnthropicClient, Console.WriteLine);

        Console.WriteLine($"\n=== Migration Complete ===");
        Console.WriteLine($"Migrated: {migrationReport.Migrated}");
        Console.WriteLine($"Skipped (no URL): {migrationReport.Skipped}");
        Console.WriteLine($"Duplicates/Already migrated: {migrationReport.Duplicates}");
        Console.WriteLine($"Needs Review: {migrationReport.NeedsReview}");
        break;

    default:
        Console.WriteLine("Usage: dotnet run -- <command>");
        Console.WriteLine("  export                              - Export Notion database to JSON");
        Console.WriteLine("  analyze                             - Analyze exported tags");
        Console.WriteLine("  suggest-taxonomy                    - AI-suggest taxonomy expansion (review before applying)");
        Console.WriteLine("  generate-aliases                    - AI-generate tag alias mappings (review before migrating)");
        Console.WriteLine("  migrate [--classify-untagged]       - Write vault markdown files (optionally AI-classify untagged)");
        break;
}
