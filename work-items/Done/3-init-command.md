# 3 - init command

## Description

Add a `markban init` command that scaffolds a new board in the current directory. Without arguments it creates a `work-items/` folder with all standard lane directories. With `--path` it creates a custom-named directory and writes a `markban.json` setting `rootPath` to that path. Optionally accepts `--name` to set a display name in the config.

Usage:

```
markban init                              # creates work-items/ + standard lanes
markban init --path my-tasks             # creates my-tasks/ + writes markban.json with rootPath
markban init --path my-tasks --name "My Project"
markban init --dry-run                   # show what would be created, no filesystem changes
```

Idempotent -- re-running on an existing board fills in missing directories without touching existing files. Consistent with `--dry-run` support on `--commit` and `--reorder`.

---

## Acceptance Criteria

- [x] `markban init` creates `work-items/` with all 6 standard lane dirs
- [x] `markban init --path <dir>` creates custom dir and writes `markban.json` with `rootPath`
- [x] `markban init --name` adds a `name` field to `markban.json`
- [x] Re-running on existing board is safe -- no files overwritten or deleted
- [x] `--dry-run` prints planned actions without touching the filesystem
- [x] Existing `markban.json` is not overwritten; conflicting fields print a warning
- [x] Help text updated to include `init`
