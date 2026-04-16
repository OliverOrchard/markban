# 10 - rename command - update title and sync filename atomically

## Description

Add `markban rename <id|slug> "New Title"` to atomically update the H1 heading in the file and rename the file to match the new slug in a single command. Currently this requires two steps: manually editing the H1, then running `markban sanitize`. Having it as an explicit command is also safe for agents and scripts -- no risk of a partial state where the H1 and filename are out of sync.

Behaviour:
- Updates the `# N - Title` heading in the file
- Renames the file to `N-new-slug.md`
- Updates cross-references from the old slug name across all board files (same logic as `sanitize`)
- Respects the `heading.enabled` config flag (see [configurable-h1-heading]) -- if H1 is disabled, only renames the file

```
markban rename 5 "Web board switcher"
markban rename board-switcher-in-web-ui "Web board switcher"
```

---

## Acceptance Criteria

- [x] `markban rename <id> "Title"` updates H1 and renames file atomically
- [x] `markban rename <slug> "Title"` works the same way
- [x] Cross-references to the old slug are updated across all files
- [x] `markban rename <id> "Title" --dry-run` shows planned rename and ref updates without executing
- [x] Errors clearly if the ID/slug does not exist
- [x] Respects `heading.enabled: false` config
