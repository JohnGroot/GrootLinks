# GrootLinks Browsing Interface Design

## Context

The GrootLinks vault contains 1,274 migrated links as individual markdown files with YAML frontmatter (title, url, tags, created, source, needs_review). The user needs a retrieval-focused browsing interface inside Obsidian with favicons as a first-class visual element. A future phase will explore Enhanced Graph View with favicon nodes for visual exploration.

## Design Principles

- **Leverage native Obsidian tools** — use built-in search (Cmd+Shift+F), tag pane, file explorer, and bookmarks as primary navigation. The dashboard complements these, not replaces them.
- **Favicons everywhere** — site icons are essential for fast visual scanning.
- **Table-first, cards optional** — dense table view as default, card grid as a toggleable alternative.

## Plugin Stack

| Plugin | Role | Status |
|--------|------|--------|
| **Dataview** | Powers table and card queries from frontmatter | Required — community plugin |
| **Link Favicons** | Auto-displays favicons next to external URLs | Required — community plugin |
| **Homepage** | Opens dashboard note on vault launch | Required — community plugin |
| **DataCards** | Renders Dataview results as card grid (toggle view) | Required — community plugin |
| **Tag Pane** | Native sidebar for browsing taxonomy hierarchy | Already enabled — core plugin |
| **Search** | Native full-text and property search | Already enabled — core plugin |
| **Bookmarks** | Pin frequent searches and tag filters | Already enabled — core plugin |

## Dashboard Note

A single markdown note (`_dashboard.md`) at the vault root, set as the Homepage target.

### Structure

1. **Header** — title, total link count, review queue count
2. **Quick Filters** — Dataview inline queries for counts:
   - Needs Review (links with `needs_review: true`)
   - Recently Added (last 7 days)
   - By source (notion vs mcp)
3. **Table View (default)** — DataviewJS block that renders an HTML table with: favicon `<img>` tag (via Google favicon API), title as link to note, domain (extracted via JS `URL` API), tags, created date. Sorted by created date descending. Uses DataviewJS rather than plain Dataview TABLE to guarantee favicon rendering and proper domain extraction.
4. **Card Grid View** — wrapped in `<details><summary>Card View</summary>` using DataCards rendering of the same query. Collapsed by default, expandable on click. No custom CSS toggle needed.

### Frontmatter Compatibility

The existing link frontmatter already contains everything needed:
- `title` — display name
- `url` — for favicon lookup and domain extraction
- `tags` — for tag pills and filtering
- `created` — for date column and sorting
- `needs_review` — for review queue filter

No frontmatter changes required.

## CSS Snippet

A custom CSS snippet (`grootlinks-dashboard.css`) in `.obsidian/snippets/` for:

- Tag pill styling (colored badges in table/card views)
- Table column widths and row density
- Favicon sizing consistency (16x16 inline with text)
- DataviewJS table styling to match Obsidian's native table look

## Obsidian Workspace Configuration

- **Left sidebar:** File explorer + Tag pane (already configured)
- **Right sidebar:** Search + Bookmarks
- **Main pane:** Dashboard note (via Homepage plugin)
- **Saved searches:** Bookmark frequently-used search queries (e.g., `tag:#music`, `tag:#needs_review`) for one-click access from the bookmarks pane

## What We Build vs What Obsidian Provides

| Need | Solution |
|------|----------|
| Full-text search | Native Obsidian search (Cmd+Shift+F) |
| Tag browsing | Native tag pane sidebar |
| Pin frequent queries | Native bookmarks |
| Formatted link table with favicons | DataviewJS HTML table + Google Favicons API + Link Favicons plugin |
| Card grid view | DataCards plugin (in collapsible `<details>` block) |
| Review queue | Dataview query filtering `needs_review: true` |
| Landing page | Homepage plugin → `_dashboard.md` |
| Visual favicon display | Link Favicons plugin (Google provider) |

## Implementation Scope

1. Install and configure plugins: Dataview, Link Favicons, Homepage, DataCards
2. Create `_dashboard.md` with Dataview queries
3. Create CSS snippet for dashboard styling
4. Configure Homepage to target `_dashboard.md`
5. Set up workspace layout (tag pane left, bookmarks right)
6. Create a few saved bookmark searches for common tag queries
7. Test with the full 1,274-link vault

## Future Phase: Graph View

Not in scope for this phase. Future work will explore Extended Graph plugin to add favicon images to graph nodes for visual/spatial link exploration.
