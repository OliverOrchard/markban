# 24a - PR creation on commit with configurable platform CLI

## Description

When `featureBranches.enabled: true`, `--commit` needs to open a pull request after the commit/squash step. PR creation is platform-specific (GitHub, GitLab, Bitbucket, Azure DevOps) so markban delegates to the platform's own CLI rather than owning API authentication.

Config:

```json
{
  "git": {
    "featureBranches": {
      "prCommand": "gh pr create --fill"
    }
  }
}
```

`prCommand` is a shell command markban runs after pushing the branch. `--fill` on `gh` uses the commit message as the PR title/body automatically. Users can customise this to any CLI invocation -- `glab mr create`, `az repos pr create`, etc.

**Detection:** If `prCommand` is not set but `gh` is on PATH, markban can offer to use `gh pr create --fill` as a default and prompt the user to confirm. Never silently invoke a tool the user hasn't configured.

**PR title/body:** By default the PR title is the work item title and the body links back to the item slug. These can be overridden via `prTitleTemplate` and `prBodyTemplate` config keys (future extension).

---

## Acceptance Criteria

- [ ] `prCommand` is run after push when feature branches are enabled
- [ ] `prCommand` stdout/stderr is forwarded to the user
- [ ] Non-zero exit from `prCommand` is treated as an error; `--commit` reports it clearly
- [ ] `--dry-run` prints the `prCommand` that would be run without executing it
- [ ] No `prCommand` configured and no `gh` on PATH -- `--commit` completes the git steps and prints a reminder to open a PR manually
- [ ] No implicit auto-detection without user config or explicit confirmation
