# 14 - Custom commit message tags

# 15 - Custom commit message tags

## Description

The valid commit tags (`feat`, `fix`, `docs`, `style`, `refactor`, `test`, `build`, `ci`, `chore`, `revert`, `perf`) are hardcoded in `CommitCommand.cs`. Allow users to replace or extend this list via `markban.json`.

```json
{
  "commit": {
    "tags": ["feat", "fix", "docs", "chore", "refactor", "test", "build", "ci", "style", "perf", "revert"]
  }
}
```

When `commit.tags` is present it fully replaces the default list -- users who want to add a custom tag like `release` or `wip` can include the full set plus their additions. When absent, the default conventional commits list applies.

The help text for `--commit` should reflect the configured tag list at runtime, not a hardcoded string.

---

## Acceptance Criteria

- [ ] `commit.tags` in config replaces the default tag list
- [ ] `--commit` validates the tag against the configured list
- [ ] Error message on invalid tag lists the configured valid tags, not the hardcoded defaults
- [ ] Help text shows configured tags at runtime
- [ ] Default list preserved when `commit.tags` is absent
- [ ] `markban init` writes `commit.tags` with the default list in explicit defaults (see [explicit-defaults-in-init])
