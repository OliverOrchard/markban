# 11 - Configurable H1 heading - opt out via markban.json

## Description

Allow users to opt out of the auto-generated `# N - Title` H1 heading via `markban.json`. This is relevant when using a custom template that already defines its own structure, or when users prefer to manage titles themselves.

Config:

```json
{
  "heading": {
    "enabled": false
  }
}
```

Default is `true` (current behaviour, fully backward compatible).

**Affected commands:**
- `--create` -- skip writing the H1 line when creating new items
- `--sanitize` -- skip H1 parsing / renaming logic; treat filename as the sole source of truth for the slug
- `--rename` (see [rename-command]) -- only rename the file, do not touch file content
- `--git-history` -- H1 extraction for display should gracefully handle missing H1
- `--overview` / `--list` -- title extraction falls back to slug if no H1

---

## Acceptance Criteria

- [x] `heading.enabled: false` in config suppresses H1 on `--create`
- [x] `--sanitize` skips H1 rewriting when disabled
- [x] `--rename` skips H1 update when disabled
- [x] All commands that extract a display title fall back to slug when no H1 is present
- [x] Default `heading.enabled: true` preserves all existing behaviour
- [x] `markban init` writes `heading.enabled: true` in the explicit defaults output (see [explicit-defaults-in-init])
