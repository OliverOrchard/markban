# markban

<p align="center">Local-first kanban board backed by plain Markdown files in your repository.</p>

**markban** combines a CLI for automation and coding agents with a local web UI for visual board management. The name comes from **mark**down + **ban** (板, *board* in Japanese, as in kanban 看板). The folder is the board; the `.md` files are the work items.

<!-- Placeholder: replace with web UI demo video (MP4/WebM) and short CLI workflow GIF before release -->
> *Demo media placeholder: web UI walkthrough video and CLI quick-start GIF to be added.*

## Features

- **Markdown source of truth** -- each work item is a plain `.md` file in `work-items/<Lane>/`, producing clean git diffs with no database or SaaS lock-in.
- **CLI + local web UI** -- run `markban` commands and `markban web` simultaneously over the same filesystem without locking.
- **Agent-friendly queries** -- structured JSON output, compact `list --summary` queries, and targeted `show` / `search` commands keep token usage low.
- **Board hygiene and history** -- validate links and ordering with `health`, inspect `[slug]` backlinks with `references`, and undo or redo moves and reorders.
- **Git-aware workflow** -- configure lanes, WIP limits, frontmatter (`blocked`, `tags`, `dependsOn`), feature branches, and conventional-commit checks.
- **Multi-board support** -- manage multiple boards across a monorepo or portfolio from a parent `markban.json` and switch between them in the browser.

## Installation

**Homebrew (macOS / Linux)**

```bash
brew install OliverOrchard/markban/markban
```

**.NET global tool**

```bash
dotnet tool install -g markban
```

**winget (Windows)** -- planned, not yet published.

## Quick start

```bash
markban init
markban create "Add board switcher"
markban move 1 "In Progress"
markban web
```

This creates `markban.json` and `work-items/` lane directories, adds item `1` in `Todo`, moves it to `In Progress`, and opens the board at `http://localhost:5000`.

## Documentation

- [CLI Commands and File Format](docs/commands.md) -- how work-item files work and the full command reference
- [Configuration (`markban.json`)](docs/configuration.md) -- lane roles, WIP limits, git/feature-branch settings, multi-board setup, and property reference
- [Web UI](docs/web-ui.md) -- board switcher, drag-to-reorder, in-place editing, Markdown/Mermaid rendering, and themes
- [Using with AI Agents](docs/ai-agents.md) -- why markban fits agent workflows, low-token tips, and the repository APM harness
- [Custom Item Template](docs/item-template.md) -- customizing new work items with `work-items/.template.md`

## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet pack Markban.Cli/Markban.Cli.csproj -c Release -o ./nupkg
dotnet tool install -g markban --add-source ./nupkg
```

## License

MIT -- see [LICENSE](LICENSE).
