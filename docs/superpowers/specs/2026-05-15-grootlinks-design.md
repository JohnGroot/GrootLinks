# GrootLinks: Notion-to-Obsidian Link Migration & MCP Parser

## Context

A personal Links database in Notion (1,321 entries, dating back to Sept 2019) is partially tagged (89.5%) with 500+ inconsistent tags (duplicates, typos, mixed granularity). The goal is to migrate this to an Obsidian vault with a clean hierarchical taxonomy, and build a .NET 10 MCP server that accepts new links, AI-classifies them, and saves them to the vault with consistent tagging.

## Notion Source Database

- **Database ID:** `79b34536-f152-4abb-a5e6-5ffce622a0bc`
- **Properties:** Name (title), URL (url), Tags (multi_select, 500+ tags), File (files), Created (created_time), Link (url, unused)
- **Entries:** 1,321 total — 1,182 tagged, 139 untagged
- **Date range:** 2019-09-04 to 2025-05-18
- **Tag issues:** duplicates (`TikTook`/`TikTok`, `climate`/`Climate`), near-duplicates (`Clothes`/`Clothing`, `Games Studies`/`Game Studies`), typos (`Theroy`), mixed specificity (broad `Technology` alongside niche `Dimes Square`)

## Architecture

### Approach: .NET 10 MCP stdio Server

A single .NET 10 console app serving as an MCP server over stdio. Claude Code and other MCP clients spawn it as a child process. The Obsidian vault is a directory of `.md` files tracked in git.

This was chosen over HTTP API or background service alternatives because:
- MCP stdio is the native Claude Code integration path
- Simplest single-process architecture
- Trivially Dockerizable later via `dotnet publish`
- Can evolve to HTTP with minimal changes (.NET 10 Minimal API)

## Vault Structure

```
~/Dev/GrootLinks/
├── vault/
│   ├── links/                    # One .md file per link
│   │   ├── 2019/
│   │   ├── 2020/
│   │   ├── ...
│   │   └── 2025/
│   ├── _templates/
│   │   └── link.md               # Obsidian template for new links
│   └── _taxonomy/
│       ├── tags.json             # Canonical hierarchical taxonomy
│       └── tag_aliases.json      # Old Notion tag -> new tag mappings
├── src/
│   └── GrootLinks/               # .NET 10 MCP server
├── tools/
│   └── migrate/                  # One-time migration scripts/tools
├── docs/
│   └── superpowers/specs/        # Design docs
└── .gitignore
```

## Link File Format

Each link is a single `.md` file named by slugified title in a year directory:

```markdown
---
title: "Rive — a new way to design, build, and ship user interfaces"
url: https://rive.app/
tags:
  - dev-tools
  - game-dev
  - ui-design
created: 2025-04-15
source: notion
needs_review: false
---

# Rive — a new way to design, build, and ship user interfaces

A design and animation tool for interactive UI.
```

- **`source`**: `notion` for migrated, `mcp` for newly added
- **`needs_review`**: `true` for AI-classified links pending human review
- **Filename**: `YYYY/slugified-title.md` (e.g., `2025/rive-interactive-ui.md`)
- **Body**: extracted description/summary from the page content

## Tag Taxonomy

### Design Principles

- Tags are **flat kebab-case** in frontmatter (standard Obsidian practice)
- The **taxonomy tree** in `tags.json` organizes tags hierarchically for classification and browsing
- Tree can be **arbitrarily deep** — not limited to two levels
- The AI classifier can assign tags at **any depth** (leaf or branch)
- Links typically get **2-5 tags**, preferring specific over broad

### Taxonomy Structure (tags.json)

Top-level categories with nested subcategories. This is illustrative — the actual taxonomy will be built during migration by analyzing all 500+ existing tags:

```json
{
  "technology": {
    "_description": "Software, hardware, and digital infrastructure",
    "programming": {
      "dotnet": {}, "rust": {}, "python": {},
      "web-dev": { "static-sites": {}, "frontend": {} },
      "reactive-programming": {}
    },
    "ai": { "machine-learning": {}, "llm": {}, "chatgpt": {} },
    "game-dev": {
      "unity": {}, "unreal": {}, "godot": {},
      "ecs": {}, "procgen": {}, "game-engine": {}
    },
    "dev-tools": { "git": {}, "cli": {}, "ide": {} },
    "infrastructure": { "cloud": {}, "linux": {}, "networking": {}, "home-server": {} },
    "hardware": { "pc-building": {}, "ergonomics": {}, "keyboards": {}, "displays": {} },
    "security": { "infosec": {}, "privacy": {}, "passwords": {}, "surveillance": {} },
    "platforms": { "apps": {}, "saas": {}, "open-source": {} }
  },
  "culture": {
    "_description": "Arts, media, and cultural production",
    "music": {
      "techno": {}, "dj": {}, "vinyl": {}, "hifi": {},
      "streaming-music": { "spotify": {}, "bandcamp": {} }
    },
    "visual-art": { "animation": {}, "graphics": {}, "photography": {}, "ceramics": {} },
    "film": { "documentaries": {}, "directors": {} },
    "television": {},
    "literature": {
      "fiction": { "scifi": {}, "horror": {} },
      "manga": {}, "writing": {}, "authors": {}
    },
    "games": {
      "game-design": { "ludic-mystery": {}, "narratology": {} },
      "game-studies": {},
      "specific-titles": { "elden-ring": {}, "metal-gear": {}, "cyberpunk-2077": {} },
      "tabletop": { "mahjong": {} },
      "esports": {}
    },
    "fashion": { "techwear": {}, "suiting": {}, "knitwear": {}, "sneakers": {} },
    "design": { "interior-design": {}, "architecture": {}, "ui-design": {} }
  },
  "politics": {
    "_description": "Political theory, movements, and current events",
    "left-theory": {
      "socialism": {}, "anarchism": {}, "marxism": {},
      "critical-theory": { "mark-fisher": {}, "deleuze": {}, "foucault": {}, "zizek": {} },
      "cooperatives": {}
    },
    "movements": {
      "organizing": {}, "unions": {}, "mutual-aid": {},
      "decolonization": {}, "black-liberation": {}, "feminism": {}, "queer-theory": {}
    },
    "geopolitics": {
      "usa": {}, "china": {}, "korea": {}, "uk": {}, "france": {},
      "israel-palestine": {}, "latin-america": {}
    },
    "policy": { "elections": {}, "housing": {}, "climate-policy": {}, "ubi": {}, "governance": {} },
    "policing": { "police-violence": {}, "prisons": {}, "surveillance": {} }
  },
  "theory": {
    "_description": "Academic and intellectual frameworks",
    "philosophy": { "biopolitics": {}, "necropolitics": {}, "accelerationism": {} },
    "economics": { "degrowth": {}, "crypto-economics": {}, "rentierism": {}, "markets": {} },
    "complexity": { "systems": {}, "simulations": {}, "santa-fe-institute": {} },
    "ecology": { "permaculture": {}, "rewilding": {}, "solarpunk": {} },
    "urbanism": { "cities": {}, "urban-planning": {}, "exurbanism": {} }
  },
  "lifestyle": {
    "_description": "Food, health, home, and daily life",
    "food": {
      "cooking": { "baking": {}, "grilling": {}, "pasta": {} },
      "recipes": {},
      "restaurants": {},
      "coffee": {}, "drinks": { "wine": {}, "cocktails": {}, "sake": {} }
    },
    "health": { "fitness": {}, "stretches": {}, "mindfulness": {}, "sauna": {}, "bathing": {} },
    "home": {
      "cookware": {}, "furniture": {}, "glassware": {}, "barware": {},
      "candles": {}, "fragrance": {}
    },
    "outdoor": { "camping": {}, "cycling": {}, "bikepacking": {}, "foraging": {} },
    "travel": { "hotels": {}, "flights": {} }
  },
  "commerce": {
    "_description": "Shopping, products, and brands",
    "shops": {},
    "gear": { "bags": {}, "knives": {}, "watches": {}, "cameras": {} },
    "clothing": { "boots": {}, "coats": {}, "socks": {}, "athleticwear": {} },
    "tech-gear": { "phones": {}, "macbook": {}, "keyboards": {} }
  },
  "media-format": {
    "_description": "Content format/type tags",
    "podcast": {}, "video": {}, "blog": {}, "newsletter": {},
    "documentary": {}, "tutorial": {}, "lecture": {}, "interview": {},
    "pdf": {}, "wiki": {}
  },
  "internet-culture": {
    "_description": "Online communities, platforms, and digital culture",
    "social-media": { "tiktok": {}, "twitter": {}, "reddit": {}, "discord": {} },
    "web3": { "crypto": {}, "nft": {}, "dao": {}, "ethereum": {} },
    "communities": { "forums": {}, "decentralization": {}, "dark-forest": {} },
    "memes": {}
  }
}
```

### Tag Aliases (tag_aliases.json)

Maps every original Notion tag to its canonical replacement:

```json
{
  "TikTook": "tiktok",
  "TikTok": "tiktok",
  "Theroy": "critical-theory",
  "climate": "climate-policy",
  "Climate Change": "climate-policy",
  "Games Studies": "game-studies",
  "Game Studies": "game-studies",
  "Clothes": "clothing",
  "Clothing": "clothing",
  "Computer": "hardware",
  "Videogames": "games",
  "Reddit": "reddit",
  "NewModels": "critical-theory",
  "Assassins Creed": "specific-titles",
  "...": "..."
}
```

This file is the key migration artifact — AI generates initial mappings, human reviews and edits before migration runs.

## .NET 10 MCP Server

### Project Structure

```
src/GrootLinks/
├── Program.cs                    # MCP server setup, tool registration
├── Tools/
│   ├── SaveLinkTool.cs          # save_link: fetch, classify, write .md
│   ├── SearchLinksTool.cs       # search_links: full-text + tag search
│   ├── ListTagsTool.cs          # list_tags: return taxonomy tree/subtree
│   ├── RetagLinkTool.cs         # retag_link: update tags on existing link
│   └── ReviewQueueTool.cs       # review_queue: list needs_review links
├── Services/
│   ├── LinkParser.cs            # Fetch URL, extract title/description/content
│   ├── TagClassifier.cs         # Claude API call to classify content against taxonomy
│   ├── VaultWriter.cs           # Write/update Obsidian .md files with frontmatter
│   └── TaxonomyService.cs       # Load, query, and validate against tags.json
├── Models/
│   ├── Link.cs                  # Link data model
│   ├── Taxonomy.cs              # Taxonomy tree model
│   └── TagAlias.cs              # Alias mapping model
└── GrootLinks.csproj
```

### MCP Tools

| Tool | Input | Output | Behavior |
|------|-------|--------|----------|
| `save_link` | `url`, optional `tags[]`, optional `title` | Saved file path + assigned tags | Fetches page content, AI-classifies against taxonomy, writes `.md` with `needs_review: true` |
| `search_links` | `query`, optional `tags[]`, optional `needs_review` | List of matching links | Full-text search across vault + tag filtering |
| `list_tags` | optional `category` | Taxonomy tree or subtree | Returns the tag hierarchy for browsing |
| `retag_link` | `file_path`, `tags[]`, optional `clear_review` | Updated link | Updates frontmatter tags, optionally sets `needs_review: false` |
| `review_queue` | optional `limit` | List of links needing review | Returns all links with `needs_review: true` |

### Dependencies

- `ModelContextProtocol` — official .NET MCP SDK for stdio server
- `Anthropic` SDK — for Claude API calls (tag classification)
- `HtmlAgilityPack` — HTML content extraction from URLs
- `YamlDotNet` — YAML frontmatter parsing/writing
- `System.Text.Json` — taxonomy and config file handling

### AI Classification Flow

1. **Fetch**: HTTP GET the URL, extract text via HtmlAgilityPack (title, meta description, first ~2000 chars of visible body text)
2. **Classify**: Send extracted content to Claude with the full taxonomy tree as context
   - Prompt: "Given this content and taxonomy, assign 2-5 tags. Prefer specific leaf tags over broad parents. If the content spans multiple categories, tag across categories. Return only tag slugs from the taxonomy."
3. **Merge**: If user provided manual tags, merge with AI suggestions (user tags take priority)
4. **Write**: Generate `.md` file with frontmatter, save to `vault/links/YYYY/slug.md`

## Migration Pipeline

### Phase 1: Export & Analyze

1. Export all 1,321 Notion entries to a local JSON file via Notion API
2. Analyze all 500+ existing tags — frequency, co-occurrence, duplicates
3. AI-generate initial `tag_aliases.json` mapping old → new
4. **Human review gate**: user edits the alias file before proceeding

### Phase 2: Transform & Classify

5. Apply alias mappings to all tagged entries
6. For 139 untagged entries: fetch URL content, AI-classify against taxonomy
7. For entries where the mapped tags seem insufficient: AI-enhance with additional tags
8. All AI-classified entries get `needs_review: true`

### Phase 3: Write & Verify

9. Write all 1,321 `.md` files to `vault/links/YYYY/slug.md`
10. Generate migration report: tag mapping stats, entries per year, review queue size
11. Git commit the vault

### Idempotency

Migration is idempotent — entries matched by URL are skipped on re-run. A `migration_log.json` tracks what was processed and when.

## Configuration

The MCP server reads config from `vault/_taxonomy/tags.json` and environment:

- `ANTHROPIC_API_KEY` — for Claude API classification calls
- `GROOTLINKS_VAULT_PATH` — path to vault directory (defaults to `../vault` relative to binary)

## Verification Plan

1. **Migration**: Run export, verify JSON has all 1,321 entries. Run alias mapping, verify tag counts. Write vault, verify file count and structure.
2. **MCP Server**: Start via `dotnet run`, connect from Claude Code, test `save_link` with a URL, verify `.md` file created with correct tags and `needs_review: true`.
3. **Round-trip**: Save a link via MCP, find it via `search_links`, retag it via `retag_link`, verify `needs_review` cleared.
4. **Obsidian**: Open vault in Obsidian, verify links render correctly, tags are searchable.
