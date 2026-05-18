using Anthropic;
using Anthropic.Core;
using GrootLinks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

var vaultPath = Environment.GetEnvironmentVariable("GROOTLINKS_VAULT_PATH")
    ?? throw new InvalidOperationException(
        "GROOTLINKS_VAULT_PATH environment variable is required. Set it to the absolute path of your vault directory.");

var taxonomyPath = Path.Combine(vaultPath, "_taxonomy", "tags.json");
var aliasesPath = Path.Combine(vaultPath, "_taxonomy", "tag_aliases.json");

builder.Services.AddSingleton(new TaxonomyService(taxonomyPath, aliasesPath));
builder.Services.AddSingleton(new VaultWriter(vaultPath));

builder.Services.AddHttpClient<LinkParser>();

builder.Services.AddSingleton(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is required");
    return new AnthropicClient(new ClientOptions { ApiKey = apiKey });
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
