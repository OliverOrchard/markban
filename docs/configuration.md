# Configuration (`markban.json`)

Place a `markban.json` file in your project root (next to `work-items/`) to configure your board. Running `markban init` writes an explicit default `markban.json`:

```json
{
  "rootPath": "./work-items",
  "name": "My Project",
  "lanes": [
    { "name": "Todo",        "ordered": true,  "type": "ready" },
    { "name": "In Progress", "ordered": true,  "wip": 3 },
    { "name": "Testing",     "ordered": true },
    { "name": "Done",        "ordered": true,  "type": "done" },
    { "name": "Ideas",       "ordered": false, "pickable": false },
    { "name": "Rejected",    "ordered": false, "pickable": false }
  ],
  "heading": {
    "enabled": true
  },
  "slugs": {
    "casing": "kebab"
  },
  "commit": {
    "maxMessageLength": 72,
    "tags": ["feat", "fix", "docs", "style", "refactor", "test", "build", "ci", "chore", "revert", "perf"]
  },
  "git": {
    "enabled": true,
    "featureBranches": {
      "enabled": false,
      "mainBranch": "main",
      "branchPrefix": "feature/",
      "commitStrategy": "single",
      "pullOnStart": true,
      "checkoutOnDone": true,
      "prCommand": ""
    }
  },
  "web": {
    "port": 5000
  },
  "blocked": {
    "enabled": true
  },
  "tags": {
    "enabled": true
  },
  "dependsOn": {
    "enabled": true
  },
  "subItemReorder": "warn",
  "customFrontmatter": [],
  "history": {
    "enabled": true,
    "maxEntries": 50
  }
}
```

## Multi-board example

A parent `markban.json` can point at multiple child boards across a monorepo or portfolio:

```json
{
  "name": "Platform",
  "rootPath": "./work-items",
  "boards": [
    { "name": "Backend", "path": "services/api" },
    { "name": "Frontend", "path": "services/web" }
  ]
}
```

In multi-board mode:
- If the root directory also contains a board (`rootPath` or `./work-items`), the root board is automatically included first in the board list using top-level `name` (or the directory name).
- Each entry's `path` may point to a directory containing its own `markban.json`, a directory containing `work-items/`, or a directory directly containing lane folders.
- Each board gets a URL-safe `key` derived from its `name` (must be unique across `boards`).
- `markban overview` prints progress per board.
- The [Web UI](web-ui.md) shows a board switcher in the header, and board-aware API endpoints accept `?board=<key>`.
- Use `markban board remove <key> [--dry-run]` (and `markban board add <name> <path>`) to manage the `boards` array from the CLI.

---

## Property reference

### Top-level properties

| Property | Type | Default | Description |
|---|---|---|---|
| `rootPath` | string | `"./work-items"` | Path to the board directory, relative to `markban.json`. |
| `name` | string | directory name | Display name for the board. Also used to derive the root board's key when `boards` is configured. |
| `lanes` | array | standard 6 lanes | Lane definitions. When omitted, `Todo`, `In Progress`, `Testing`, `Done`, `Ideas`, and `Rejected` are used. |
| `boards` | array | `[]` | Optional list of `{ "name": string, "path": string }` entries for multi-board mode. |
| `heading` | object | `{ "enabled": true }` | Controls automatic H1 heading generation (`# {id} - Title`) in new work items. |
| `slugs` | object | `{ "casing": "kebab" }` | Controls filename slug casing for `create` and `rename`. |
| `commit` | object | see below | Configures commit message validation and allowed conventional-commit tags for `markban commit`. |
| `git` | object | see below | Configures git integration and feature-branch workflow for `markban start` and `markban commit`. |
| `web` | object | `{ "port": 5000 }` | Default port for `markban web`. |
| `blocked` | object | `{ "enabled": true }` | Enables the `blocked` frontmatter field and `markban block` command. |
| `tags` | object | `{ "enabled": true }` | Enables the `tags` frontmatter field, `markban tag`, `create --tags`, and `list --filter-tag`. |
| `dependsOn` | object | `{ "enabled": true }` | Enables the `dependsOn` frontmatter field, `markban depends-on`, and dependency checks in `next` and `health`. |
| `subItemReorder` | `"warn"` \| `"block"` \| `"force"` | `"warn"` | Controls reordering a lettered sub-item (e.g. `32a`) in the web UI and in `markban reorder` (where sub-item IDs may appear in `<order>`; `--yes` skips the `Continue? [y/N]` prompt, `--dry-run` previews the detach/renumber plan, and a non-interactive stdin counts as "no" without `--yes`). `"warn"` (default, written by `markban init`): dragging or reordering a sub-item asks for confirmation, then detaches it from its parent and renumbers it as a standalone item. `"block"`: sub-item cards cannot be dragged, the API rejects the request with 400, and the CLI exits non-zero without changes. `"force"`: the sub-item is detached and reordered without asking. Any other value is an error. The parent's other sub-items stay attached to the parent. |
| `customFrontmatter` | array | `[]` | Array of `{ "name": string, "default": any }` objects defining custom frontmatter fields populated by `markban create` and `create --set key=value`. |
| `history` | object | `{ "enabled": true, "maxEntries": 50 }` | Configures the persisted undo/redo journal stored in `.markban/history.json` (outside the work-item lanes and gitignored). |

### Lane properties (`lanes[*]`)

| Property | Type | Default | Description |
|---|---|---|---|
| `name` | string | -- | Directory name of the lane inside `rootPath`. |
| `ordered` | boolean | `false` | `true` = numbered filenames (`1-slug.md`); `false` = slug-only filenames (`slug.md`). |
| `type` | `"ready"` \| `"done"` | omit | `"ready"` = commitment lane (`create` defaults here, `next` and `start` read from here). `"done"` = terminal lane (`commit` moves here, `Done` items sort highest-number-first). Omit for neutral workflow or holding lanes. At most one `"ready"` and one `"done"` lane may be configured. |
| `pickable` | boolean | `true` | `false` excludes this lane from `next`, `progress`, and `create` defaults. Use for holding lanes (`Ideas`, `Rejected`). |
| `wip` | number | none | Maximum items allowed in this lane. Enforced by `move` and `create` unless `--override-wip` is passed. |

Array order in `lanes` drives `markban progress` and `markban start` (advancing an item to the next pickable workflow lane). Use `markban lane rename <old-name> <new-name> [--dry-run]` to atomically rename a lane directory and update its `name` in `markban.json`.

> **Planned lane property:** Lane-level `defaultCollapsed` (boolean) for setting the initial collapsed/hidden state of a lane in the Web UI is planned alongside Web UI `localStorage` preference persistence and is not yet active.

### `heading` and `slugs` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `heading.enabled` | boolean | `true` | When `true`, `markban create` writes `# {id} - Title` (or `# Title` in unordered lanes) as the first line of the Markdown body, and `move` / `reorder` / `rename` keep the heading number synchronized. Set to `false` to omit the H1 heading. |
| `slugs.casing` | `"kebab"` \| `"snake"` \| `"camel"` \| `"pascal"` | `"kebab"` | Casing rule used when generating filenames from item titles during `create` and `rename`. |

### `commit` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `commit.maxMessageLength` | number | `72` | Maximum length (in characters) of the `--message` value passed to `markban commit`. |
| `commit.tags` | string[] | `["feat", "fix", "docs", "style", "refactor", "test", "build", "ci", "chore", "revert", "perf"]` | Allowed conventional-commit tags for `markban commit --tag <tag>`. |

### `git` and `git.featureBranches` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `git.enabled` | boolean | `true` | Run `git add / commit / push` on `markban commit`. Set to `false` to manage git separately. |
| `git.featureBranches.enabled` | boolean | `false` | Enable feature-branch workflow in `markban start` and `markban commit`. |
| `git.featureBranches.mainBranch` | string | `"main"` | Base branch to branch from on `start` and compare/return to on `commit`. |
| `git.featureBranches.branchPrefix` | string | `"feature/"` | Prefix for branches created by `markban start` (`<branchPrefix><id>-<slug>`). |
| `git.featureBranches.commitStrategy` | `"single"` \| `"multiple"` \| `"squash"` | `"single"` | How `markban commit` handles commits on the feature branch: `"single"` stages and commits (warns if prior commits exist), `"multiple"` commits dirty changes if any and pushes all commits, `"squash"` soft-resets to `mainBranch` and creates one squashed commit (`--force-with-lease` if already pushed). |
| `git.featureBranches.pullOnStart` | boolean | `true` | Run `git pull` on `mainBranch` before creating a feature branch in `markban start` (can be skipped per invocation with `--no-pull`). |
| `git.featureBranches.checkoutOnDone` | boolean | `true` | After pushing in `markban commit`, checkout `mainBranch` and run `git pull`. |
| `git.featureBranches.prCommand` | string | `""` | Optional shell command executed after pushing a feature branch in `markban commit` (for example, `gh pr create --fill`). |

### `blocked`, `tags`, `dependsOn`, and `customFrontmatter` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `blocked.enabled` | boolean | `true` | Enable `blocked` frontmatter support and `markban block`. |
| `tags.enabled` | boolean | `true` | Enable `tags` frontmatter support, `markban tag`, `create --tags`, and `list --filter-tag`. |
| `dependsOn.enabled` | boolean | `true` | Enable `dependsOn` frontmatter support, `markban depends-on`, and dependency checks in `next` and `health`. |
| `customFrontmatter[*].name` | string | -- | Field name written into YAML frontmatter when set via `create --set <name>=<value>` or when `default` is specified. |
| `customFrontmatter[*].default` | string \| number \| boolean \| null | omit | Optional default value written into frontmatter for every newly created item. |

### `history` properties

Shared by the CLI (`markban undo`, `markban redo`, `markban undo --list`, `markban redo --list`, plus `--dry-run`) and the web API (`GET /api/history`, `POST /api/undo`, `POST /api/redo`). Before undoing or redoing, markban verifies that every affected file still matches its recorded SHA-256 content hash; if any file was edited, moved, or deleted since, undo/redo refuses, names the files, and changes nothing.

| Property | Type | Default | Description |
|---|---|---|---|
| `history.enabled` | boolean | `true` | Record successful non-dry-run `reorder` and `move` operations in `.markban/history.json`. When `false`, nothing is recorded and `undo`/`redo` report that history is disabled. |
| `history.maxEntries` | number | `50` | Maximum entries kept in the history stacks (minimum `1`); oldest entries are dropped when exceeded. |
