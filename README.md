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

**Cross-references** use `[slug]` or `WI-N` syntax between items: `[redesign-header]` or `WI-5` links to any item with that slug or ID. The `--sanitize` command auto-converts `WI-N` to `[slug]` form.

## Installation

### dotnet tool (recommended)

Once published to NuGet:

```bash
dotnet tool install -g markban
```

### Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/OliverOrchard/markban
cd markban
dotnet pack Markban.Cli/Markban.Cli.csproj -c Release -o ./nupkg
dotnet tool install -g markban --add-source ./nupkg
```

### winget / brew

Planned — not yet available.

## Usage

```
markban --list                             List all work items as JSON
markban --list --summary                   ID, slug, status only (saves tokens)
markban --list --folder Todo               Filter to a lane
markban --next                             Show highest priority Todo item
markban --search "terms"                   Ranked search across slugs and IDs
markban --search "terms" --full            Also scan full Markdown body content
markban --move <id|slug> <lane>            Move an item between lanes
markban --create "Title"                   Create a new work item
markban --create "Title" --priority        Insert at top of Todo
markban --create "Title" --after <id>      Insert after a specific item
markban --create "Title" --sub-item --parent <id>  Create a sub-item
markban --reorder <lane> <order>           Reorder by comma-separated IDs
markban --reorder <lane> <order> --no-sub-items  Suppress sub-item grouping
markban --reorder <lane> <order> --dry-run Preview without changing files
markban --commit <id|slug> --tag feat --message "add login"
markban --commit <id|slug> --tag feat --message "msg" --dry-run
markban --check-links                      Find broken [slug] cross-references
markban --check-links --include-ideas      Also check Ideas and Rejected lanes
markban --references <slug>                Show what references this item
markban --overview                         Compact progress summary
markban --sanitize                         Fix Unicode and old ref formats
markban --git-history <file>               Work item activity from git log
markban web                                Start the web board UI
markban web --port 8080 --no-open
markban --help
```

## Web UI

`markban web` starts a local web server and opens your browser to the Kanban board. The CLI and web UI can run simultaneously — an agent can use the CLI while you view the board in the browser. They share the same filesystem and never lock each other out.

## Using with AI agents

markban is designed to work well as a tool for coding agents (Claude, Copilot, etc.) alongside a human on the same board.

- Use `--summary` to return only ID, slug, and status — avoids flooding the agent's context window with full Markdown bodies
- Use `--search` before creating items to avoid duplicates
- Use `--check-links` after bulk edits to validate cross-references
- Use `--commit --dry-run` to validate a commit before executing it — always do this first
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
  "git": {
    "enabled": true
  },
  "web": {
    "port": 5000
  }
}
```

Git integration is enabled by default. Set `git.enabled` to `false` or pass `--no-git` to `markban --commit` to skip the git add/commit/push steps.

## License

MIT — see [LICENSE](LICENSE).
