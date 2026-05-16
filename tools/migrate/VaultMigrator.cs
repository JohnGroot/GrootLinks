using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using GrootLinks.Models;
using GrootLinks.Services;

namespace GrootLinks.Migrate;

public class VaultMigrator
{
    private readonly VaultWriter _writer;
    private readonly TaxonomyService _taxonomy;
    private readonly TagClassifier? _classifier;
    private readonly string _migrationLogPath;

    public VaultMigrator(VaultWriter writer, TaxonomyService taxonomy, string migrationLogPath, TagClassifier? classifier = null)
    {
        _writer = writer;
        _taxonomy = taxonomy;
        _classifier = classifier;
        _migrationLogPath = migrationLogPath;
    }

    public async Task<MigrationReport> MigrateAsync(
        List<NotionEntry> entries,
        LinkParser? parser = null,
        AnthropicClient? anthropicClient = null,
        Action<string>? log = null)
    {
        var report = new MigrationReport();
        var migrationLog = await LoadMigrationLogAsync();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                log?.Invoke($"SKIP (no URL): {entry.Title}");
                report.Skipped++;
                continue;
            }

            if (migrationLog.ContainsKey(entry.Url))
            {
                log?.Invoke($"ALREADY-MIGRATED: {entry.Title}");
                report.Duplicates++;
                continue;
            }

            var resolvedTags = _taxonomy.ResolveAliases(entry.Tags);
            var needsReview = false;

            if (resolvedTags.Count == 0)
            {
                if (_classifier != null)
                {
                    try
                    {
                        if (parser != null)
                        {
                            log?.Invoke($"AI-CLASSIFY (fetch): {entry.Title}");
                            var page = await parser.FetchAndParseAsync(entry.Url);
                            resolvedTags = await _classifier.ClassifyAsync(page.Title, page.Description, page.BodyText);
                            await Task.Delay(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"FETCH-FAIL (using title): {entry.Title} - {ex.Message}");
                    }

                    if (resolvedTags.Count == 0 && anthropicClient != null)
                    {
                        try
                        {
                            log?.Invoke($"AI-CLASSIFY (title-only): {entry.Title}");
                            var taxonomyJson = _taxonomy.GetTaxonomyTreeJson();
                            var prompt = TagClassifier.BuildClassificationPrompt(
                                entry.Title, null, entry.Title, taxonomyJson);

                            var response = await anthropicClient.Messages.Create(new MessageCreateParams
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

                            resolvedTags = TagClassifier.ParseClassificationResponse(text)
                                .Where(t => _taxonomy.IsValidTag(t)).ToList();
                            await Task.Delay(500);
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"CLASSIFY-FAIL: {entry.Title} - {ex.Message}");
                        }
                    }
                }

                needsReview = true;
            }

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

            migrationLog[entry.Url] = filePath;

            log?.Invoke($"OK: {entry.Title} -> {Path.GetFileName(filePath)} [{string.Join(", ", resolvedTags)}]");
            report.Migrated++;
            if (needsReview) report.NeedsReview++;

            if (report.Migrated % 25 == 0)
                await SaveMigrationLogAsync(migrationLog);
        }

        await SaveMigrationLogAsync(migrationLog);
        return report;
    }

    private async Task<Dictionary<string, string>> LoadMigrationLogAsync()
    {
        if (!File.Exists(_migrationLogPath)) return new();
        var json = await File.ReadAllTextAsync(_migrationLogPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
    }

    private async Task SaveMigrationLogAsync(Dictionary<string, string> log)
    {
        var json = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_migrationLogPath, json);
    }
}

public class MigrationReport
{
    public int Migrated { get; set; }
    public int Skipped { get; set; }
    public int Duplicates { get; set; }
    public int NeedsReview { get; set; }
}
