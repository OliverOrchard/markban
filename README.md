# markban

**markban** is a markdown board — a file-based work item tracker with a CLI and a local web UI.

The name comes from **mark**down + **ban** (板, *board* in Japanese, as in kanban 看板). Your work items live as plain Markdown files in folders. The folder *is* the board.

## How it works

Work items are `.md` files inside a `work-items/` directory at your project root:

```
work-items/
  Todo/
    1-implement-login.md
    2-add-tests.md
  In Progress/
    3-redesign-header.md
  Testing/
  Done/
  Ideas/
  Rejected/
```

**Number = priority.** Lower number = higher priority. Renaming a file changes its priority — no database, no metadata, just filenames.

**Sub-items** use letter suffixes: `12a-`, `12b-`, `12c-` group related tasks under a parent.

**Cross-references** use `[slug]` or `WI-N` syntax between items: `[redesign-header]` or `WI-5` links to any item with that slug or ID. The `sanitize` command auto-converts `WI-N` to `[slug]` form.

## Installation

### brew (macOS / Linux)

```bash
brew install OliverOrchard/markban/markban
```

### dotnet tool

Planned — not yet available on NuGet.

### Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/OliverOrchard/markban
cd markban
dotnet pack Markban.Cli/Markban.Cli.csproj -c Release -o ./nupkg
dotnet tool install -g markban --add-source ./nupkg
```

### winget

Planned — not yet available.

## Usage

```
markban list                               List all work items as JSON
markban list --summary                     ID, slug, status only (saves tokens)
markban list --folder Todo                 Filter to a lane
markban next                               Show highest priority Todo item
markban next-id                            Print the next safe work item number
markban show <id|slug>                     Show a specific work item
markban search "terms"                     Ranked search across slugs and IDs
markban search "terms" --full              Also scan full Markdown body content
markban move <id|slug> <lane>              Move an item between lanes
markban create "Title"                     Create a new work item
markban create "Title" --priority          Insert at top of Todo
markban create "Title" --after <id>        Insert after a specific item
markban create "Title" --sub-item --parent <id>  Create a sub-item
markban reorder <lane> <order>             Reorder by comma-separated IDs
markban reorder <lane> <order> --no-sub-items  Suppress sub-item grouping
markban reorder <lane> <order> --dry-run   Preview without changing files
markban commit <id|slug> --tag feat --message "add login"
markban commit <id|slug> --tag feat --message "msg" --dry-run
markban health                             Run all board diagnostics
markban health check-links                 Find broken [slug] cross-references
markban health check-links --include-ideas Also check Ideas and Rejected lanes
markban health check-order                 Check for duplicate or missing IDs
markban references <slug>                  Show what references this item
markban overview                           Compact progress summary
markban sanitize                           Fix Unicode and old ref formats
markban git-history <file>                 Work item activity from git log
markban web                                Start the web board UI
markban web --port 8080 --no-open
markban help
markban --help
```

## Web UI

`markban web` starts a local web server and opens your browser to the Kanban board. The CLI and web UI can run simultaneously — an agent can use the CLI while you view the board in the browser. They share the same filesystem and never lock each other out.

## Using with AI agents

markban is designed to work well as a tool for coding agents (Claude, Copilot, etc.) alongside a human on the same board.

- Use `--summary` to return only ID, slug, and status — avoids flooding the agent's context window with full Markdown bodies
- Use `search` before creating items to avoid duplicates
- Use `health check-links` after bulk edits to validate cross-references
- Use `commit --dry-run` to validate a commit before executing it — always do this first
- The agent can use the CLI while you watch the board update live in the browser — no coordination needed, no locking

## Custom item template

By default, new items are created with a `## Description` and `## Acceptance Criteria` section. To use your own template, add a `.template.md` file inside your `work-items/` directory:

```markdown
## Context

Why this work matters.

## Tasks

- [ ] Task 1
- [ ] Task 2

## Acceptance Criteria

- [ ] Criterion 1
```

The file becomes the body of every new item (after the auto-generated `# {id} - Title` heading). Sub-items use the same template.

## Configuration

Place a `markban.json` in your project root to configure:

```json
{
  "rootPath": "./work-items",
  "name": "My Project",
  "lanes": [
    { "name": "Todo",        "ordered": true,  "type": "ready" },
    { "name": "In Progress", "ordered": true },
    { "name": "Testing",     "ordered": true },
    { "name": "Done",        "ordered": true,  "type": "done" },
    { "name": "Ideas",       "ordered": false, "pickable": false },
    { "name": "Rejected",    "ordered": false, "pickable": false }
  ],
  "git": {
    "enabled": true
  },
  "web": {
    "port": 5000
  }
}
```

### Property reference

**Top-level**

| Property | Type | Description |
|---|---|---|
| `rootPath` | string | Path to the board directory, relative to `markban.json`. Defaults to `work-items/` in the same directory. |
| `name` | string | Display name for the board. |
| `lanes` | array | Lane definitions. When omitted, the standard lanes above are used. |

**Lane properties** (`lanes[*]`)

| Property | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | Directory name of the lane. |
| `ordered` | boolean | — | `true` = numbered filenames (`1-slug.md`); `false` = slug-only filenames. |
| `type` | `"ready"` \| `"done"` | omit | `"ready"` = commitment lane (`create` defaults here, `next` reads from here). `"done"` = terminal lane (`commit` moves here). Omit for neutral lanes. |
| `pickable` | boolean | `true` | `false` excludes this lane from `next` and `create` defaults. Use for holding lanes (`Ideas`, `Rejected`). |
| `wip` | number | none | Maximum items allowed in this lane. No property = no limit. |

Array order drives the `progress` command (advance an item one lane forward).

**`git` properties**

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | boolean | `true` | Run `git add / commit / push` on `markban commit`. Set to `false` to manage git separately. |

**`git.featureBranches` properties** *(planned — not yet implemented)*

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | boolean | `false` | Activates feature-branch workflow mode. |
| `mainBranch` | string | `"main"` | Base branch for new feature branches. |
| `commitStrategy` | `"single"` \| `"multiple"` \| `"squash"` | `"squash"` | How commits are handled on `markban commit`. |
| `pullOnStart` | boolean | `true` | Pull `mainBranch` before creating a feature branch. |
| `checkoutOnDone` | boolean | `true` | Check out and pull `mainBranch` after a PR is created. |

**Naming conventions**

- All property names are **camelCase**.
- No `is` prefix on booleans (`pickable`, not `isPickable`).
- Enum string values are **lowercase** (`"ready"`, `"done"`).

## License

MIT — see [LICENSE](LICENSE).
