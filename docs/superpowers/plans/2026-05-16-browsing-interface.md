# Browsing Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a retrieval-focused browsing interface inside the Obsidian vault with favicons, a DataviewJS table, a collapsible card grid, and native Obsidian search/tag integration.

**Architecture:** Four community plugins (Dataview, Link Favicons, Homepage, DataCards) installed into the vault's `.obsidian/plugins/` directory. A single dashboard markdown note (`_dashboard.md`) at the vault root with DataviewJS queries. One CSS snippet for styling. Workspace JSON updated for optimal sidebar layout.

**Tech Stack:** Obsidian community plugins (JSON config), DataviewJS (JavaScript), CSS, JSON workspace config

**Spec:** `docs/superpowers/specs/2026-05-16-browsing-interface-design.md`

---

## File Structure

| File | Purpose |
|------|---------|
| `vault/.obsidian/community-plugins.json` | Create — register community plugin IDs |
| `vault/.obsidian/plugins/dataview/manifest.json` | Create — Dataview plugin manifest |
| `vault/.obsidian/plugins/dataview/data.json` | Create — Dataview plugin settings |
| `vault/.obsidian/plugins/obsidian-link-favicon/manifest.json` | Create — Link Favicons plugin manifest |
| `vault/.obsidian/plugins/obsidian-link-favicon/data.json` | Create — Link Favicons plugin settings |
| `vault/.obsidian/plugins/homepage/manifest.json` | Create — Homepage plugin manifest |
| `vault/.obsidian/plugins/homepage/data.json` | Create — Homepage plugin settings |
| `vault/.obsidian/plugins/data-cards/manifest.json` | Create — DataCards plugin manifest |
| `vault/.obsidian/plugins/data-cards/data.json` | Create — DataCards plugin settings |
| `vault/.obsidian/snippets/grootlinks-dashboard.css` | Create — dashboard styling |
| `vault/.obsidian/appearance.json` | Modify — enable CSS snippet |
| `vault/.obsidian/workspace.json` | Modify — sidebar layout with tag pane |
| `vault/_dashboard.md` | Create — main dashboard note |
| `.gitignore` | Modify — add `.superpowers/` |

**Important note on plugin installation:** Obsidian community plugins consist of `manifest.json`, `main.js`, `styles.css`, and `data.json`. We cannot distribute `main.js`/`styles.css` via git — these are downloaded by Obsidian's plugin manager. Our plan creates the config files so Obsidian knows which plugins to expect, but the user must **open the vault in Obsidian and install the plugins via Settings → Community Plugins** on first launch. The `data.json` files pre-configure each plugin's settings so no manual configuration is needed after install.

---

### Task 1: Add `.superpowers/` to `.gitignore`

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Add `.superpowers/` to `.gitignore`**

Add to the end of `/Users/johngroot/Dev/GrootLinks/.gitignore`:

```
# Superpowers brainstorm sessions
.superpowers/
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add .superpowers/ to gitignore"
```

---

### Task 2: Register Community Plugins

**Files:**
- Create: `vault/.obsidian/community-plugins.json`

- [ ] **Step 1: Create the community plugins registry**

Create `vault/.obsidian/community-plugins.json`:

```json
[
  "dataview",
  "obsidian-link-favicon",
  "homepage",
  "data-cards"
]
```

These IDs match the official Obsidian plugin registry names.

- [ ] **Step 2: Commit**

```bash
cd vault && git add .obsidian/community-plugins.json && git commit -m "feat: register community plugins for browsing interface"
```

---

### Task 3: Configure Dataview Plugin

**Files:**
- Create: `vault/.obsidian/plugins/dataview/manifest.json`
- Create: `vault/.obsidian/plugins/dataview/data.json`

- [ ] **Step 1: Create plugin directory and manifest**

Create `vault/.obsidian/plugins/dataview/manifest.json`:

```json
{
  "id": "dataview",
  "name": "Dataview",
  "description": "Complex data views for the data-obsessed.",
  "isDesktopOnly": false
}
```

- [ ] **Step 2: Create plugin settings**

Create `vault/.obsidian/plugins/dataview/data.json`:

```json
{
  "enableDataviewJs": true,
  "enableInlineDataviewJs": true,
  "enableInlineDataview": true,
  "tableIdColumnName": "",
  "tableGroupColumnName": "Group",
  "showResultCount": true,
  "renderNullAs": "—",
  "warnOnEmptyResult": false,
  "refreshEnabled": true,
  "refreshInterval": 5000
}
```

Key setting: `enableDataviewJs: true` — required for our DataviewJS table block in the dashboard.

- [ ] **Step 3: Commit**

```bash
cd vault && git add .obsidian/plugins/dataview/ && git commit -m "feat: configure Dataview plugin with DataviewJS enabled"
```

---

### Task 4: Configure Link Favicons Plugin

**Files:**
- Create: `vault/.obsidian/plugins/obsidian-link-favicon/manifest.json`
- Create: `vault/.obsidian/plugins/obsidian-link-favicon/data.json`

- [ ] **Step 1: Create plugin directory and manifest**

Create `vault/.obsidian/plugins/obsidian-link-favicon/manifest.json`:

```json
{
  "id": "obsidian-link-favicon",
  "name": "Link Favicons",
  "description": "See the favicon for a linked website.",
  "isDesktopOnly": false
}
```

- [ ] **Step 2: Create plugin settings**

Create `vault/.obsidian/plugins/obsidian-link-favicon/data.json`:

```json
{
  "provider": "google",
  "showLink": true,
  "showAliased": true,
  "showInEditor": true,
  "overrideExternalLinkIcon": true
}
```

Key setting: `provider: "google"` — Google's favicon API has no rate limits and returns 16x16 icons. `overrideExternalLinkIcon: true` replaces the default external link icon with the actual site favicon.

- [ ] **Step 3: Commit**

```bash
cd vault && git add .obsidian/plugins/obsidian-link-favicon/ && git commit -m "feat: configure Link Favicons plugin with Google provider"
```

---

### Task 5: Configure Homepage Plugin

**Files:**
- Create: `vault/.obsidian/plugins/homepage/manifest.json`
- Create: `vault/.obsidian/plugins/homepage/data.json`

- [ ] **Step 1: Create plugin directory and manifest**

Create `vault/.obsidian/plugins/homepage/manifest.json`:

```json
{
  "id": "homepage",
  "name": "Homepage",
  "description": "Open a specified note, canvas, or workspace on startup.",
  "isDesktopOnly": false
}
```

- [ ] **Step 2: Create plugin settings**

Create `vault/.obsidian/plugins/homepage/data.json`:

```json
{
  "version": 3,
  "defaultNote": "_dashboard",
  "useMoment": false,
  "autoCreate": true,
  "openOnStartup": true,
  "hasRibbonIcon": true,
  "openMode": "Replace all open notes",
  "manualOpenMode": "Keep open notes",
  "view": "Default view",
  "revertView": true,
  "autoScroll": true
}
```

Key setting: `defaultNote: "_dashboard"` — points to our dashboard note. `autoCreate: true` ensures Obsidian doesn't error if the file is missing. `openOnStartup: true` makes it the landing page.

- [ ] **Step 3: Commit**

```bash
cd vault && git add .obsidian/plugins/homepage/ && git commit -m "feat: configure Homepage plugin targeting _dashboard"
```

---

### Task 6: Configure DataCards Plugin

**Files:**
- Create: `vault/.obsidian/plugins/data-cards/manifest.json`
- Create: `vault/.obsidian/plugins/data-cards/data.json`

- [ ] **Step 1: Create plugin directory and manifest**

Create `vault/.obsidian/plugins/data-cards/manifest.json`:

```json
{
  "id": "data-cards",
  "name": "DataCards",
  "description": "Transform Dataview queries into beautiful card layouts.",
  "isDesktopOnly": false
}
```

- [ ] **Step 2: Create plugin settings**

Create `vault/.obsidian/plugins/data-cards/data.json`:

```json
{
  "defaultPreset": "compact",
  "lazyLoading": true,
  "showRefreshButton": true
}
```

Key setting: `defaultPreset: "compact"` — dense card layout that shows more cards per row, matching the retrieval-focused use case. `lazyLoading: true` for performance with 1,274 links.

- [ ] **Step 3: Commit**

```bash
cd vault && git add .obsidian/plugins/data-cards/ && git commit -m "feat: configure DataCards plugin with compact preset"
```

---

### Task 7: Create CSS Snippet

**Files:**
- Create: `vault/.obsidian/snippets/grootlinks-dashboard.css`
- Modify: `vault/.obsidian/appearance.json`

- [ ] **Step 1: Create the snippets directory**

```bash
mkdir -p /Users/johngroot/Dev/GrootLinks/vault/.obsidian/snippets
```

- [ ] **Step 2: Create the CSS snippet**

Create `vault/.obsidian/snippets/grootlinks-dashboard.css`:

```css
/* GrootLinks Dashboard Styles */

/* DataviewJS table — match Obsidian native table look */
.grootlinks-table {
  width: 100%;
  border-collapse: collapse;
}

.grootlinks-table th {
  text-align: left;
  padding: 6px 10px;
  border-bottom: 2px solid var(--background-modifier-border);
  color: var(--text-muted);
  font-size: 0.85em;
  font-weight: 600;
}

.grootlinks-table td {
  padding: 5px 10px;
  border-bottom: 1px solid var(--background-modifier-border);
  vertical-align: middle;
}

.grootlinks-table tr:hover {
  background-color: var(--background-modifier-hover);
}

/* Favicon sizing in table */
.grootlinks-table .gl-favicon {
  width: 16px;
  height: 16px;
  vertical-align: middle;
  margin-right: 6px;
  border-radius: 2px;
}

/* Title link styling */
.grootlinks-table .gl-title a {
  color: var(--text-normal);
  text-decoration: none;
  font-weight: 500;
}

.grootlinks-table .gl-title a:hover {
  color: var(--text-accent);
}

/* Domain column */
.grootlinks-table .gl-domain {
  color: var(--text-muted);
  font-size: 0.85em;
}

/* Tag pills */
.gl-tag {
  display: inline-block;
  padding: 1px 7px;
  margin: 1px 2px;
  border-radius: 10px;
  font-size: 0.75em;
  background-color: var(--background-modifier-border);
  color: var(--text-muted);
}

/* Date column */
.grootlinks-table .gl-date {
  color: var(--text-muted);
  font-size: 0.85em;
  white-space: nowrap;
}

/* Card view details toggle styling */
.gl-card-section summary {
  cursor: pointer;
  color: var(--text-muted);
  font-size: 0.9em;
  padding: 8px 0;
}

.gl-card-section summary:hover {
  color: var(--text-accent);
}
```

- [ ] **Step 3: Enable the CSS snippet in appearance.json**

Read `vault/.obsidian/appearance.json`, then update it to:

```json
{
  "enabledCssSnippets": [
    "grootlinks-dashboard"
  ]
}
```

If `appearance.json` already has other keys, merge `enabledCssSnippets` into the existing object.

- [ ] **Step 4: Commit**

```bash
cd vault && git add .obsidian/snippets/grootlinks-dashboard.css .obsidian/appearance.json && git commit -m "feat: add dashboard CSS snippet with table and tag pill styles"
```

---

### Task 8: Create the Dashboard Note

**Files:**
- Create: `vault/_dashboard.md`

This is the core deliverable. The dashboard has three sections: header with inline stats, DataviewJS table (default view), and a collapsible DataCards card grid.

- [ ] **Step 1: Create the dashboard note**

Create `vault/_dashboard.md`:

````markdown
---
cssclasses:
  - grootlinks-dashboard
---

# Links Dashboard

`$= dv.pages('"links"').length` links · `$= dv.pages('"links"').where(p => p.needs_review).length` need review · `$= dv.pages('"links"').where(p => p.source === "mcp").length` via MCP

---

```dataviewjs
const pages = dv.pages('"links"')
  .sort(p => p.created, 'desc');

const rows = pages.map(p => {
  let domain = "";
  try {
    domain = new URL(p.url).hostname.replace(/^www\./, "");
  } catch(e) {
    domain = p.url || "";
  }

  const favicon = `<img class="gl-favicon" src="https://www.google.com/s2/favicons?domain=${domain}&sz=16" alt="">`;

  const title = `<span class="gl-title">${favicon}<a href="${p.url}" class="external-link" rel="noopener">${p.title || p.file.name}</a></span>`;

  const domainCell = `<span class="gl-domain">${domain}</span>`;

  const tags = (p.tags || [])
    .map(t => `<span class="gl-tag">${t}</span>`)
    .join(" ");

  const date = p.created
    ? `<span class="gl-date">${p.created}</span>`
    : `<span class="gl-date">—</span>`;

  return [title, domainCell, tags, date];
});

dv.paragraph(
  `<table class="grootlinks-table">
    <thead><tr>
      <th>Title</th>
      <th>Domain</th>
      <th>Tags</th>
      <th>Added</th>
    </tr></thead>
    <tbody>${rows.map(r =>
      `<tr><td>${r[0]}</td><td>${r[1]}</td><td>${r[2]}</td><td>${r[3]}</td></tr>`
    ).join("")}</tbody>
  </table>`
);
```

<details class="gl-card-section">
<summary>▦ Card View</summary>

```dataview
TABLE WITHOUT ID
  ("![favicon|16](https://www.google.com/s2/favicons?domain=" + regexreplace(url, "https?://([^/]+).*", "$1") + "&sz=16)") AS " ",
  "[" + title + "](" + url + ")" AS Title,
  tags AS Tags,
  created AS Added
FROM "links"
SORT created DESC
```

</details>
````

**Design notes:**
- The DataviewJS block renders a full HTML table with inline favicons from Google's API, proper domain extraction via JS `URL`, and CSS-styled tag pills.
- The card view uses a plain Dataview TABLE inside a `<details>` block — DataCards will automatically transform this if configured, or it renders as a standard table fallback.
- Inline Dataview queries (`$=`) in the header show live counts.
- The `cssclasses: grootlinks-dashboard` frontmatter scopes CSS if needed in the future.

- [ ] **Step 2: Verify the file exists and looks correct**

```bash
head -10 /Users/johngroot/Dev/GrootLinks/vault/_dashboard.md
```

Expected: YAML frontmatter with `cssclasses` followed by the `# Links Dashboard` heading.

- [ ] **Step 3: Commit**

```bash
cd vault && git add _dashboard.md && git commit -m "feat: create links dashboard with DataviewJS table and card view"
```

---

### Task 9: Update Workspace Layout

**Files:**
- Modify: `vault/.obsidian/workspace.json`

Update the workspace to put the tag pane in the right sidebar (easily accessible for browsing) and keep file explorer + search + bookmarks in the left sidebar.

- [ ] **Step 1: Update workspace.json**

Read the current `vault/.obsidian/workspace.json`, then replace it with this layout:

```json
{
  "main": {
    "id": "main-tabs",
    "type": "split",
    "children": [
      {
        "id": "main-tab-group",
        "type": "tabs",
        "children": [
          {
            "id": "dashboard-leaf",
            "type": "leaf",
            "state": {
              "type": "markdown",
              "state": {
                "file": "_dashboard.md",
                "mode": "preview"
              },
              "icon": "lucide-layout-dashboard",
              "title": "Links Dashboard"
            }
          }
        ]
      }
    ],
    "direction": "vertical"
  },
  "left": {
    "id": "left-sidebar",
    "type": "split",
    "children": [
      {
        "id": "left-tabs",
        "type": "tabs",
        "children": [
          {
            "id": "file-explorer-leaf",
            "type": "leaf",
            "state": {
              "type": "file-explorer",
              "state": {
                "sortOrder": "alphabetical"
              },
              "icon": "lucide-folder-closed",
              "title": "Files"
            }
          },
          {
            "id": "search-leaf",
            "type": "leaf",
            "state": {
              "type": "search",
              "state": {
                "query": "",
                "matchingCase": false,
                "explainSearch": false,
                "collapseAll": false,
                "extraContext": false,
                "sortOrder": "alphabetical"
              },
              "icon": "lucide-search",
              "title": "Search"
            }
          },
          {
            "id": "bookmarks-leaf",
            "type": "leaf",
            "state": {
              "type": "bookmarks",
              "state": {},
              "icon": "lucide-bookmark",
              "title": "Bookmarks"
            }
          }
        ],
        "currentTab": 0
      }
    ],
    "direction": "horizontal",
    "width": 280
  },
  "right": {
    "id": "right-sidebar",
    "type": "split",
    "children": [
      {
        "id": "right-tabs",
        "type": "tabs",
        "children": [
          {
            "id": "tag-pane-leaf",
            "type": "leaf",
            "state": {
              "type": "tag",
              "state": {
                "sortOrder": "frequency",
                "useHierarchy": true
              },
              "icon": "lucide-tags",
              "title": "Tags"
            }
          },
          {
            "id": "backlink-leaf",
            "type": "leaf",
            "state": {
              "type": "backlink",
              "state": {
                "collapseAll": false,
                "extraContext": false,
                "sortOrder": "alphabetical"
              },
              "icon": "links-coming-in",
              "title": "Backlinks"
            }
          }
        ],
        "currentTab": 0
      }
    ],
    "direction": "horizontal",
    "width": 280,
    "collapsed": false
  },
  "left-ribbon": {
    "hiddenItems": {
      "switcher:Open quick switcher": false,
      "graph:Open graph view": false,
      "canvas:Create new canvas": false,
      "daily-notes:Open today's daily note": false,
      "templates:Insert template": false,
      "command-palette:Open command palette": false
    }
  },
  "active": "dashboard-leaf",
  "lastOpenFiles": [
    "_dashboard.md"
  ]
}
```

Key changes from current workspace:
- Main pane opens `_dashboard.md` in preview mode (reading view)
- Left sidebar: file explorer (default tab), search, bookmarks
- Right sidebar: **tag pane** (default tab, sorted by frequency, hierarchical), backlinks — **expanded by default** (`collapsed: false`)

- [ ] **Step 2: Commit**

```bash
cd vault && git add .obsidian/workspace.json && git commit -m "feat: update workspace layout with tag pane and dashboard"
```

---

### Task 10: Manual Plugin Installation and Verification

This task requires opening Obsidian — it cannot be automated.

- [ ] **Step 1: Open the vault in Obsidian**

Open `/Users/johngroot/Dev/GrootLinks/vault/` as an Obsidian vault.

- [ ] **Step 2: Enable community plugins**

Go to Settings → Community Plugins → Turn on community plugins (if not already enabled).

- [ ] **Step 3: Install the four plugins**

In Settings → Community Plugins → Browse, search for and install each:
1. **Dataview** by Michael Brenan
2. **Link Favicons** by Johannes Theiner
3. **Homepage** by novov
4. **DataCards** by Sophokles187

Enable each plugin after installation.

- [ ] **Step 4: Verify the dashboard loads**

Close and reopen the vault. The Homepage plugin should automatically open `_dashboard.md`. Verify:
- The DataviewJS table renders with rows
- Favicons appear as 16x16 images next to link titles
- Domain column shows extracted hostnames
- Tags display as pills
- The `<details>` card view section expands on click
- The tag pane shows the taxonomy hierarchy in the right sidebar

- [ ] **Step 5: Verify native tools work**

- Click a tag in the right sidebar tag pane → Obsidian should filter/navigate
- Use Cmd+Shift+F to search → full-text search across all link files
- Use Cmd+O (quick switcher) → find links by title

- [ ] **Step 6: Create saved bookmark searches**

In Obsidian's Search (left sidebar), run these searches and bookmark each (right-click → Bookmark this search):
1. `tag:#music` — music links
2. `tag:#programming` — programming links
3. `[needs_review: true]` — review queue
4. `tag:#shops` — shopping links

These appear in the Bookmarks tab for one-click access.

- [ ] **Step 7: Commit any Obsidian-generated files**

After Obsidian installs the plugins, it creates `main.js` and `styles.css` in each plugin directory. Commit these so the vault is fully portable:

```bash
cd vault && git add .obsidian/plugins/ .obsidian/bookmarks.json && git commit -m "feat: add installed plugin files and saved bookmarks"
```

---

## Verification Checklist

After completing all tasks, verify end-to-end:

- [ ] Vault opens with `_dashboard.md` as the landing page
- [ ] DataviewJS table shows all 1,274 links with favicons, domains, tags, dates
- [ ] Card view section expands when clicking the `<details>` toggle
- [ ] Tag pane in right sidebar shows hierarchical taxonomy with counts
- [ ] Clicking a tag in the tag pane filters/navigates correctly
- [ ] Native search (Cmd+Shift+F) finds links by content
- [ ] Bookmarked searches appear in the Bookmarks tab
- [ ] Link Favicons shows site icons next to URLs throughout the vault (not just dashboard)
- [ ] CSS snippet is active (check Settings → Appearance → CSS Snippets)
