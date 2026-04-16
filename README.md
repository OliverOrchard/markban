# markban

**markban** is a local-first kanban board for software projects, backed by plain Markdown files in your repo.

It combines:
- a **CLI** for automation and coding agents
- a **local web UI** for visual board management
- **git-friendly diffs** because every work item is a normal `.md` file
- **no SaaS, no database, no lock-in**

markban is designed for developers who want their backlog to live with the codebase and be usable by both humans and AI agents.

The name comes from **mark**down + **ban** (板, *board* in Japanese, as in kanban 看板). The folder is the board. The files are the work items.

## Why markban?

markban sits in a useful middle ground:

- **More structured than ad-hoc markdown notes**
- **More local and git-native than hosted project boards**
- **More visual than CLI-only task managers**
- **More agent-friendly than typical kanban tools**

If you want a board that lives in your repo, works offline, diffs cleanly in git, and can be operated by both humans and coding agents, markban is built for that.

## What makes markban different?

- **Markdown is the source of truth** -- each work item is a real file
- **CLI + browser UI** over the same filesystem
- **Structured JSON output** for automation and coding agents
- **Compact queries** like `list --summary` to reduce token usage
- **Board hygiene tools** such as `health`, `references`, `sanitize`, and `reorder`
- **Git-aware workflow** with `commit` and `git-history`
- **Configurable lanes, WIP limits, and progress flow**
- **Multi-board support** for portfolios, monorepos, or split project areas

## Quick workflow

```text
markban init
markban create "Add board switcher"
markban next
markban move 1 "In Progress"
markban show 1
markban web
markban progress 1
markban commit 1 --tag feat --message "add board switcher" --dry-run
```

That gives you markdown work items in `work-items/`, a CLI for scripted or agent-driven changes, and a live local board in the browser for human review.

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

**Number = priority.** Lower number = higher priority. Renaming a file changes its priority -- no database, no metadata, just filenames.

**Sub-items** use letter suffixes: `12a-`, `12b-`, `12c-` group related tasks under a parent.

**Cross-references** use `[slug]` or `WI-N` syntax between items: `[redesign-header]` or `WI-5` links to any item with that slug or ID. The `sanitize` command auto-converts `WI-N` to `[slug]` form.

## Installation

### brew (macOS / Linux)

```bash
brew install OliverOrchard/markban/markban
```

### dotnet tool

Planned -- not yet available on NuGet.

### Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/OliverOrchard/markban
cd markban
dotnet pack Markban.Cli/Markban.Cli.csproj -c Release -o ./nupkg
dotnet tool install -g markban --add-source ./nupkg
```

### winget

Planned -- not yet available.

## Command overview

```text
markban init [--path <dir>] [--name "Board Name"] [--dry-run]
markban list [--folder <lane>] [--summary]
markban next
markban show <id|slug>
markban search <term> [--full]
markban move <id|slug> <lane> [--override-wip]
markban progress <id|slug> [--dry-run]
markban next-id
markban reorder <lane> <order> [--no-sub-items] [--dry-run] [--start-number <n>]
markban create "Title" [--lane <lane>] [--after <id>] [--priority] [--override-wip]
markban create "Title" --sub-item --parent <id>
markban rename <id|slug> "New Title" [--dry-run]
markban overview
markban sanitize
markban health [check-links|check-order] [--include-ideas] [--fix]
markban references <slug|id> [--include-ideas]
markban git-history <file>
markban commit <id|slug> --tag <tag> --message "msg" [--dry-run]
markban web [--port <port>] [--no-open]
markban help
```

## Web UI

`markban web` starts a local web server and opens your browser to the board. The CLI and web UI can run simultaneously -- an agent can use the CLI while you watch the board in the browser. They share the same filesystem and never lock each other out.

In multi-board mode, the web UI exposes a board switcher so you can move between configured boards without leaving the browser.

## Using with AI agents

markban is deliberately designed to work well with coding agents.

### Why it works well

- `list` returns structured JSON that is easy for tools and agents to consume
- `list --summary` returns only `id`, `slug`, and `status`, which reduces token usage dramatically
- `list --folder <lane>` narrows the result set before the agent reads full item bodies
- `search <term>` provides ranked discovery across IDs and slugs, with `--full` available when body search is worth the extra tokens
- `show <id|slug>` lets an agent fetch one item in full only when needed
- `health check-links`, `references`, and `sanitize` help validate bulk edits and keep the board coherent
- `commit --dry-run` gives a safe preview before any git action
- the CLI can be used by an agent while a human watches the same board update live in the browser

### Low-token workflow tips

When an agent is operating on a large board, prefer this flow:

1. `markban list --folder "In Progress" --summary`
2. `markban next` or `markban search "term"`
3. `markban show <id|slug>` only for the specific item being worked on
4. `markban health check-links` after edits that touch references

That keeps the agent in compact JSON until it needs the full markdown body.

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

Place a `markban.json` in your project root to configure your board:

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
  "git": {
    "enabled": true
  },
  "web": {
    "port": 5000
  }
}
```

### Multi-board example

A parent `markban.json` can also point at multiple boards:

```json
{
  "boards": [
    { "name": "Backend", "path": "services/api" },
    { "name": "Frontend", "path": "services/web" }
  ]
}
```

In multi-board mode:
- `overview` prints progress per board
- the web UI shows a board switcher
- board-aware API calls can target a specific board

### Property reference

**Top-level**

| Property | Type | Description |
|---|---|---|
| `rootPath` | string | Path to the board directory, relative to `markban.json`. Defaults to `work-items/` in the same directory. |
| `name` | string | Display name for the board. |
| `lanes` | array | Lane definitions. When omitted, the standard lanes above are used. |
| `boards` | array | Optional list of child boards for multi-board mode. |

**Lane properties** (`lanes[*]`)

| Property | Type | Default | Description |
|---|---|---|---|
| `name` | string | -- | Directory name of the lane. |
| `ordered` | boolean | -- | `true` = numbered filenames (`1-slug.md`); `false` = slug-only filenames. |
| `type` | `"ready"` \| `"done"` | omit | `"ready"` = commitment lane (`create` defaults here, `next` reads from here). `"done"` = terminal lane (`commit` moves here). Omit for neutral lanes. |
| `pickable` | boolean | `true` | `false` excludes this lane from `next` and `create` defaults. Use for holding lanes (`Ideas`, `Rejected`). |
| `wip` | number | none | Maximum items allowed in this lane. No property = no limit. |

Array order drives the `progress` command (advance an item one lane forward).

**`git` properties**

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | boolean | `true` | Run `git add / commit / push` on `markban commit`. Set to `false` to manage git separately. |

**Naming conventions**

- All property names are **camelCase**.
- No `is` prefix on booleans (`pickable`, not `isPickable`).
- Enum string values are **lowercase** (`"ready"`, `"done"`).

## Planned

- `git.featureBranches` workflow mode
- PR creation on commit
- winget distribution

## License

MIT -- see [LICENSE](LICENSE).
