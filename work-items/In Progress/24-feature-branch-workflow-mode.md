# 24 - Feature branch workflow mode

## Description

An optional mode where markban manages a full feature branch lifecycle per work item. Enabled via `markban.json`. When active, `--start <id>` creates and checks out a branch named after the item slug, and `--commit` closes the loop with a PR and returns to main.

```json
{
  "git": {
    "featureBranches": {
      "enabled": true,
      "mainBranch": "main",
      "commitStrategy": "squash",
      "pullOnStart": true,
      "checkoutOnDone": true
    }
  }
}
```

**`--start <id|slug>`** (new command):
- Errors clearly if working tree is dirty
- Checks out `<mainBranch>` and pulls latest (default, skip with `--no-pull` or `pullOnStart: false`)
- Creates and checks out a branch: `feature/<slug>` (or `<id>-<slug>` -- configurable prefix)
- Moves the item to the next lane in config order automatically (same as `--progress`)
- When feature branches are **disabled**, `--start` still works -- it just moves the item to the next lane without creating a branch

**`--commit`** in feature branch mode:
- Commit/squash per configured strategy
- Push branch + open PR via `prCommand` (see [pr-creation-on-commit])
- Moves item to `Done` lane automatically
- Checks out `<mainBranch>` and pulls (if `checkoutOnDone: true`)

**Commit strategies:**
- `single` -- one commit per item. `--commit` stages, commits, and opens PR. Warns if there are already commits on the branch beyond main.
- `multiple` -- user commits freely during work. `--commit` opens a PR from the existing branch commits with no additional commit. History preserved as-is.
- `squash` -- user commits freely. `--commit` squashes all branch commits into one clean commit with the configured message format, then opens PR.

**`checkoutOnDone: true`** -- after the PR is created, automatically runs `git checkout <mainBranch> && git pull`. User lands back on a clean, up-to-date main ready for the next `--start`.

**PR creation is delegated to the platform CLI** -- see [pr-creation-on-commit]. markban does not own API auth.

---

## Acceptance Criteria

- [ ] `featureBranches.enabled: false` (default) -- `--start` still moves item to the next lane in config order, just skips branch creation
- [ ] `--start` when feature branches disabled does not error -- moves item to next lane in config order silently
- [ ] `--start <id>` errors with clear message if working tree is dirty
- [ ] `--start <id>` checks out main and pulls latest before branching (default)
- [ ] `--start --no-pull` skips the pull step
- [ ] `pullOnStart: false` config permanently disables pull on start
- [ ] `--start <id>` creates `feature/<slug>` branch and moves item to In Progress atomically
- [ ] `commitStrategy: single` -- `--commit` does one commit + PR + moves to Done
- [ ] `commitStrategy: multiple` -- `--commit` opens PR from existing commits + moves to Done
- [ ] `commitStrategy: squash` -- `--commit` squashes all branch commits then opens PR + moves to Done
- [ ] `checkoutOnDone: true` checks out and pulls main after PR creation
- [ ] `mainBranch` config drives all branch base and checkout targets
- [ ] `markban init` writes feature branch defaults as disabled in explicit config (see [explicit-defaults-in-init])
- [ ] `--dry-run` on `--start` and `--commit` shows planned git commands and lane moves
