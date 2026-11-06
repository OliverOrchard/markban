# Using with AI Agents

markban is designed so humans and coding agents can operate on the same backlog in the repository without a hosted service or external API credentials.

## Why markban?

markban sits in a practical middle ground:

- **More structured than ad-hoc Markdown notes** -- explicit lanes, numeric priority, WIP limits, and frontmatter metadata (`blocked`, `tags`, `dependsOn`).
- **More local and git-native than hosted project boards** -- work items travel with branches, commits, and pull requests.
- **More visual than CLI-only task managers** -- a live local web UI over the same folder.
- **More agent-friendly than typical kanban tools** -- deterministic CLI commands and compact JSON output.

## Why it works well for coding agents

- `markban list` returns structured JSON that tools and agents can parse directly.
- `markban list --summary` (or `-s`) returns only `id`, `slug`, and `status`, reducing token usage on large boards.
- `markban list --folder <lane>` (or `-f <lane>`) and `--filter-tag <tag>` narrow the result set before the agent reads full item bodies.
- `markban search <term>` provides ranked discovery across IDs, slugs, and titles, with `--full` available when scanning full Markdown content is worth the extra tokens.
- `markban show <id|slug>` fetches one item in full only when needed.
- `markban health`, `markban references`, and `markban sanitize` validate cross-references, `dependsOn` links, and ordering after edits.
- `markban commit ... --dry-run` (and `--dry-run` on `init`, `progress`, `reorder`, `rename`, `start`, `lane`, `board`, and `undo`/`redo`) provides a safe preview before mutating state.
- An agent can drive the CLI while a human watches the same board update live in `markban web`.

## Low-token workflow tips

When an agent is operating on a large board, prefer this sequence:

1. `markban list --folder "In Progress" --summary`
2. `markban next` or `markban search "term"`
3. `markban show <id|slug>` only for the specific item being worked on
4. `markban health check-links` after edits that touch `[slug]` or `dependsOn` references

This keeps the agent working with compact JSON summaries until it needs the full Markdown body of a single item.

## Repository agent harness

This repository's own development instructions and skills are managed by [APM](https://microsoft.github.io/apm/) from `apm.yml` and `.apm/`. Run `apm install --frozen` and `apm compile` to reproduce the Copilot and Antigravity harness. Edit the `.apm/` sources, not the deployed copies.

Copilot provides `developer` and `orchestrator` custom agents. Both Copilot and Antigravity share the `markban-developer` and `markban-orchestrator` skills. The orchestrator protocol uses one canonical board writer and one git worktree per worker, preserves human `Testing` approval before `Done`, and supports optional Herdr terminal coordination. It is not an unattended scheduler or an atomic task-claim service.

See the [harness guide](../.apm/README.md) for entry points, setup, isolation rules, and orchestration options.
