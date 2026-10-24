---
name: markban-orchestrator
description: Coordinate an explicitly requested batch of markban board tasks across Copilot or Antigravity workers. Use for board triage, task assignment, isolated worktrees, result collection, and human-gated integration; not for automatically delegating ordinary coding requests.
---

# markban orchestrator

You coordinate; workers implement. Read `markban-developer` for project architecture and the assigned worker contract. This skill defines a protocol, not a scheduler service or a sandbox. Never claim a worker exists until its runtime reports successful startup in the assigned directory.

## Authorize and inspect

1. Identify the canonical checkout and its absolute `work-items` path. Run `git status --short`, `git worktree list --porcelain`, and `markban help`. Check installed capabilities instead of inventing flags.
2. Ask the human to authorize the batch: task scope, maximum concurrent workers, Copilot / Antigravity / mixed worker selection, worktree parent directory, committed baseline, and whether checkpoint commits are allowed. Honor any supplied values; do not silently choose an unlimited batch.
3. Require one coordinator for this board. Ask if ownership is uncertain. This convention is not an atomic claim lock; multiple independent coordinators require a durable claim service before running concurrently.
4. Read compact board state using `markban --root "<canonical-work-items-path>" list --summary`, then `show <slug>` for candidates. Inspect In Progress and Testing first. An active item belongs to its existing owner until explicitly reassigned. Existing Testing items block dispatch unless the human explicitly overrides the gate for this batch.
5. Pick highest-priority eligible tasks in Todo within the authorized scope. Inspect full `blocked` and `dependsOn` metadata; unresolved dependencies, overlapping files, parent/sub-item coupling, shared migrations, or unclear criteria require serial execution or clarification. A dependency is not satisfied just because a worker is idle or its item is in Testing.

Never infer ownership from `markban next`: it is a query, not an atomic claim. Never dispatch the same slug twice.

## Claim and isolate

For each approved, non-overlapping task:

1. Re-read canonical status and slug immediately before claiming it. The coordinator alone runs `markban --root "<canonical-work-items-path>" move <slug> "In Progress"`; re-read with `show` to verify the mutation. Do not bypass WIP limits.
2. Record the assignment before startup: slug, baseline SHA, branch, absolute worktree, canonical board path, allowed files, acceptance criteria, worker runtime, and eventually its runtime handle. Keep this run ledger in coordinator session storage, outside tracked source, and make it available for recovery. It is not a second task board.
3. Use a distinct branch and worktree for every implementation worker. Example PowerShell, after substituting approved paths, slug, and exact baseline SHA:

   ```powershell
   git worktree add -b "agent/<slug>" "C:\Dev\markban-workers\<slug>" <baseline-sha>
   ```

   Verify the registered worktree, branch, and `HEAD` before launching. Do not use `--force`, reuse another worker's directory or branch, or silently fall back to the main checkout.
4. The baseline must already contain this harness. Uncommitted changes are not copied to a worktree. If the migration or prerequisite code is not committed, stop and obtain approval for a scoped baseline commit; never commit the human's unrelated changes.
5. Each worker uses its own checkout's `.agents` skills and rules. If deployment is needed, run `apm install --frozen` and `apm compile` there, not concurrently in the coordinator's root. Workers must not regenerate or commit unrelated harness artifacts.
6. Provide the full assignment, not just "pick up next". Require `markban-developer` assigned worker mode, and forbid canonical or snapshot board mutations. Explicitly prohibit branch switching, pushes, releases, and nested implementation workers.

On startup failure, inspect what actually started, record the blocker and notify the human. Do not pretend the task is running or silently roll back the lane if there may already be live work. Retry only after resolving ownership.

## Select a runtime adapter

### Herdr: portable terminal coordination

Use this adapter only when explicitly requested and `HERDR_ENV=1`. Load the installed `herdr` skill and follow its current guardrails and command discovery. Do not access the user's focused Herdr session from an outside agent.

Both orchestrator and workers may be Copilot or Antigravity CLI; Herdr is the shared control surface. Discover supported kinds with `herdr agent`; documented kinds are `copilot` and `agy`. The Antigravity desktop application is not a CLI pane.

After creating the approved worktree, create a sibling pane in that directory without taking focus. Honor a requested split direction; otherwise inspect `herdr pane layout` and choose right for a wide pane or down for a narrow/tall one. Example PowerShell for a wide pane:

```powershell
if ($Env:HERDR_ENV -ne "1") { throw "Not running in a Herdr-managed pane." }
$splitJson = herdr pane split --current --direction right --cwd "C:\Dev\markban-workers\<slug>" --no-focus
if ($LASTEXITCODE -ne 0) { throw "Herdr pane creation failed." }
$split = $splitJson | ConvertFrom-Json
$workerPane = $split.result.pane.pane_id
if (-not $workerPane) { throw "Herdr did not return a pane ID." }
herdr agent start <unique-worker-name> --kind copilot --pane $workerPane
if ($LASTEXITCODE -ne 0) { throw "Worker startup failed; inspect the pane before retrying." }
```

For Antigravity substitute `--kind agy`. Use a name matching Herdr's documented naming constraints, not a potentially long task slug. Read the startup response and inspect blocked authentication or trust prompts with the human. Do not add permission-bypass flags.

Submit the assignment using `herdr agent prompt <name> "<assignment>"`. To start a parallel batch, submit each authorized assignment without a blocking wait, then collect results via `agent get`, bounded `agent wait`, and `agent read`. Follow Herdr's activity/readiness rules; an idle state immediately after submission is not proof the prompt ran. Do not blindly retry a timeout or stalled prompt because it may already have been delivered.

Antigravity can be idle while background jobs/subagents remain active. Require the worker's explicit completion report and confirm no writers remain before integrating or cleaning up.

### Native runtime

Use a harness's supported subagent/session mechanism only after verifying it can bind each implementation worker to the approved worktree. A custom-agent persona, copied prompt, or subagent name alone does not isolate files. If independent cwd or lifecycle control is unavailable, use Herdr or have the human start a separate session in each worktree.

Do not invent a cross-tool `spawn_agent` API, call Copilot-only workflow tools from Antigravity, or assume a native fleet isolates writes automatically.

## Collect and stop at human review

1. Require an explicit worker report: task slug, branch/worktree/base SHA, changed files, criteria evidence, exact commands and results, blockers, and remaining background jobs.
2. Inspect that actual worktree's diff, status, and any approved commits against the recorded base; a report alone is insufficient. Check there are no board edits, unrelated changes, or undeclared file overlap. Failed or incomplete work remains In Progress with its blocker reported.
3. Apply criteria checkboxes only to the canonical item's Markdown after verifying evidence. Board lane/status operations go through markban. Only then move that item to Testing and re-read its state.
4. Testing closes dispatch of new tasks. Already assigned workers may finish an explicitly approved parallel batch; do not silently refill their slots. Present each branch/worktree and evidence for human testing. Never mark Done on a worker's assertion.
5. Before any integration, ask for human acceptance and commit approval. Keep the board in the coordinator checkout; integrate into a separate clean integration worktree when the coordinator checkout has unrelated changes. Review one task at a time. Checkpoint commits require scoped staging and explicit approval; exclude copied board files. Cherry-picking approved commits or applying reviewed patches is a human-selected integration strategy, not an automatic action.
6. Run combined validation after integration; individually passing branches can still conflict. Only accepted, integrated work can become Done. Preserve the existing version-bump, release, and dry-run approval workflow; never invoke `markban commit` in a dirty/shared coordinator checkout because it stages everything and pushes.
7. Retain worker branches/worktrees until integration and human confirmation. Stop only processes you created, using their handles or PIDs. Remove only the exact approved, clean worktree with `git worktree remove "<path>"`; never force-remove it or delete a parent directory. Leave blocked/dirty work intact and explain why.

## Recovery

On restart, reconcile the ledger against canonical board state, `git worktree list --porcelain`, branch/base SHAs, and live runtime handles before dispatch. Never assume a stale pane ID is still owned by this run. Ask before reassigning an orphaned item. Worktrees isolate source files and indexes, not shared Git refs, credentials, ports, external services, CPU, or account quotas; serialize integration and global configuration changes.
