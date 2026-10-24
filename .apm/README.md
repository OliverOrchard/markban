# Agent harness

The source of truth for this repository's agent harness is `apm.yml` and `.apm`.
APM deploys the same development and orchestration skills to Copilot and
Antigravity. It manages context and dependencies, not agent processes.

## Install and maintain

Install [Microsoft APM](https://microsoft.github.io/apm/getting-started/installation/)
first. Both targets are declared explicitly: Antigravity is not auto-detected
and is not included in APM's `all` target.

From the repository root in PowerShell:

```powershell
apm install --frozen
apm compile
```

After editing the manifest or local primitives, accept the changes with an
ordinary install, regenerate context, and inspect the diff:

```powershell
apm compile --validate
apm install
apm compile
apm audit --ci
```

Commit the source, `apm.lock.yaml`, and deployed artifacts together. Do not
hand-edit generated files. `apm_modules` is an ignored dependency cache.
`apm install --frozen` follows the lockfile; use `apm audit --ci` to detect
modified or missing deployed files.

Deployment and discovery were exercised with the locally installed APM 0.30.0
and a session-local, checksum-verified APM 0.35.0. No global tools were upgraded.
Use one APM version consistently when regenerating artifacts; upgrading versions
can change ownership metadata. After an upgrade, use an ordinary install and
audit before expecting frozen replay to be sufficient. Do not use `--force` to
silence ownership conflicts.

APM may warn that no organization policy repository was found. This project
does not declare an organization policy; a successful local integrity audit is
not a claim of organization-policy enforcement.

## Sources and deployed surfaces

| Source | Copilot | Antigravity |
|---|---|---|
| `.apm\instructions\*.instructions.md` | `.github\instructions\*.instructions.md` | `.agents\rules\*.md` |
| `.apm\skills\markban-developer\SKILL.md` | `.agents\skills\markban-developer\SKILL.md` | Same shared skill |
| `.apm\skills\markban-orchestrator\SKILL.md` | `.agents\skills\markban-orchestrator\SKILL.md` | Same shared skill |
| `.apm\agents\*.agent.md` | `.github\agents\*.agent.md` | Not supported; invoke the skills instead |
| Pinned Herdr dependency | `.agents\skills\herdr\SKILL.md` | Same shared skill |
| `apm compile` | Root `AGENTS.md` | Root `AGENTS.md` |

The former developer agent's project knowledge, CLI guidance, coding/testing
references, distribution guidance, and human-gated workflow now live in
`markban-developer`. Its Copilot definition is a thin adapter. Model selection
and tool permissions now come from the active runtime instead of pinning an
editor-specific tool list and a particular model.

The compiled root context deliberately retains the standards under their file
pattern headings as a common fallback alongside native rules. This costs some
duplicate context but avoids depending solely on per-file-rule behavior in
different clients. Skills remain on-demand. `.github\workflows\release.yml`
is release automation, not an agent primitive, and remains unchanged.

## Entry points

Start Copilot in this repository and select `/agent orchestrator`, or launch:

```powershell
copilot --agent orchestrator
```

In Antigravity CLI (`agy`), App, or IDE, open this repository and invoke
`/markban-orchestrator`, or explicitly ask it to use the
`markban-orchestrator` skill. Use `/markban-developer` for a standalone task.
Copilot also discovers both shared skills; `/agent developer` retains the
existing developer entry point.

Example request, after the harness is committed and a baseline is chosen:

> Use markban-orchestrator with Herdr. Plan a batch of at most two independent
> Todo tasks, using one worktree per task under C:\Dev\markban-workers. Use
> Copilot workers and the agreed baseline commit. Show the assignments before
> dispatch. Do not commit, push, or release.

For an Antigravity or mixed batch, change the requested worker runtime. When
Herdr is selected, start the orchestrator **inside a Herdr pane** so its process
inherits `HERDR_ENV=1`; installing the skill alone does not establish a session.
Workers need their respective installed CLI and authentication. Never set that
environment variable manually to bypass the guard.

## Orchestration choices

| Option | Portability | Isolation and tradeoff |
|---|---|---|
| **Herdr + Git worktrees (recommended)** | Either CLI can coordinate either or mixed worker CLIs | Real independent terminal sessions and source checkouts; visible approvals and logs. Herdr handles lifecycle, not atomic board claims or semantic completion. |
| Native Copilot subagents / fleet | Copilot-specific control plane | Useful for research and bounded subtasks. Verify each implementation worker's cwd; a subagent does not inherently mean a separate worktree. |
| Native Antigravity agent sessions | Antigravity-specific control plane | App offers New worktree mode. Shared skills/rules are portable, but do not assume its native spawn API or session identifiers work in Copilot. |
| A small external supervisor / MCP service | Both clients could call a shared service | Best for unattended scheduling, durable claims, retries, budgets, and recovery. Requires implementation and per-runtime adapters; APM can distribute its context/configuration but is not that service. |
| Separate clones or containers | Either runtime | Clones separate Git metadata too; containers can isolate dependencies and services. Higher setup/storage cost; canonical board ownership and reviewed integration are still needed. |

The shipped orchestrator is an **agent-driven coordination protocol**, with a
documented Herdr adapter, not a continuously running daemon, transactional claim
service, or new Markban command. No workers were dispatched and no worktrees,
task claims, commits, or releases were created by the migration.

## Why worktrees and one board writer

A worktree gives every worker a separate branch, Git index, source tree,
`bin`/`obj`, and ordinary local artifacts. Two workers in separate panes but the
same checkout can still overwrite each other's files and staging area.

Use this ownership model:

1. One coordinator owns the canonical board and records slug-to-worker
   assignments in its session ledger. Slugs survive priority changes.
2. That coordinator checks dependencies, blockers, WIP, and file overlap before
   moving an item to In Progress and dispatching it.
3. Workers implement only their assigned task in separate worktrees. Their
   copied board is a snapshot, not a competing authoritative board. Workers
   return acceptance-criteria evidence; they do not change either board.
4. The coordinator inspects diffs and checks, updates the canonical acceptance
   criteria, and moves completed items to Testing for human review.
5. Testing stops new dispatch; already assigned tasks in an explicitly approved
   batch may finish. Human-approved integration is serialized and followed by
   combined checks. Only accepted, integrated work becomes Done.

Important limits:

- `markban next` plus `markban move` is not an atomic claim. Do not run multiple
  coordinators against one board without adding real locking/claim storage.
- Worktrees start from commits. They do not inherit dirty changes from the
  coordinator. This migration and any required pending code must be included in
  an explicitly approved baseline before workers depend on them.
- Worktrees share Git refs and repository configuration, credentials, network
  services, host resources, and account quotas. Use unique ports, avoid global
  tool/config updates, and serialize integration. They are not security sandboxes.
- The current `markban commit` stages the whole checkout and pushes, as well as
  moving the item to Done. It is not a worker checkpoint command. Never run it
  in a dirty/shared coordinator checkout.
- Dependency branches must be accepted and integrated before dependent tasks
  start, unless the human explicitly approves a stacked-branch workflow.
- Herdr's idle state is not proof of successful work. In particular, Antigravity
  can return to idle while background jobs or subagents are still active.

For unattended operation, the next engineering increment is a durable claim
registry with atomic acquisition, explicit ownership/leases and reconciliation,
plus process exit/result records. Keep board writes single-owner even with that
registry. APM and Herdr alone do not supply those guarantees.

## Research references

Reviewed on 2026-10-24:

- [APM overview](https://microsoft.github.io/apm/)
- [APM package anatomy](https://microsoft.github.io/apm/concepts/package-anatomy/)
- [APM targets matrix](https://microsoft.github.io/apm/reference/targets-matrix/)
- [APM manifest](https://microsoft.github.io/apm/reference/manifest-schema/)
- [APM compilation](https://microsoft.github.io/apm/reference/cli/compile/)
- [Herdr agent skill](https://herdr.dev/docs/agent-skill/)
- [Herdr agent automation](https://herdr.dev/docs/agent-automation/)
- [Herdr agent support and lifecycle caveats](https://herdr.dev/docs/agents/)
- [Antigravity skills and discovery paths](https://antigravity.google/docs/skills)
- [Antigravity rules](https://antigravity.google/docs/rules)
- [Antigravity surfaces and worktree mode](https://antigravity.google/docs/getting-started?tab=cli)
- [Copilot CLI](https://docs.github.com/copilot/how-tos/use-copilot-agents/use-copilot-cli)

The Herdr skill dependency is pinned to `v0.9.5`, with its resolved commit and
content hashes recorded in `apm.lock.yaml`. Local runtime help was also checked
for Copilot, Antigravity CLI, Herdr, and APM; command names and permissions remain
runtime-owned.
