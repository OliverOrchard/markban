# 25b - Tags and labels via frontmatter with list filtering

## Description

Allow work items to have tags stored in frontmatter, enabling filtering across all CLI commands and visual grouping in the web UI.

```yaml
---
tags: [bug, backend, auth]
---
```

Commands:
```
markban tag <id> bug,backend        # add tags (idempotent)
markban tag <id> --remove bug       # remove a tag
markban tag <id>                    # list tags for this item
markban list --filter-tag bug       # filter board by tag
markban list --filter-tag bug,backend  # items with any of these tags
```

Tags are free-form strings. No predefined list -- users define their own taxonomy. Case-insensitive matching.

**`create` integration:** `markban create "Title" --tags bug,backend` sets tags at creation time.

**Web UI:** Tag badges on cards. Clickable to filter the board by that tag. Multi-tag filter supported.

**Agent use case:** Agents can tag items by component or type (`--tags backend,auth`) to enable targeted `--list --filter-tag` queries without scanning full content.

Depends on [frontmatter-layer] (frontmatter layer).

---

## Acceptance Criteria

- [ ] `markban tag <id> <tags>` adds tags to frontmatter (comma-separated, idempotent)
- [ ] `markban tag <id> --remove <tag>` removes a tag
- [ ] `markban tag <id>` lists tags for that item
- [ ] `markban list --filter-tag <tag>` filters items by tag
- [ ] `markban create "Title" --tags <tags>` sets tags at creation time
- [ ] Tag matching is case-insensitive
- [ ] Web UI shows tag badges on cards, clickable to filter board
- [ ] `markban list --json` includes `tags` array in output
- [ ] Feature is gated by `tags: { "enabled": true }` in `markban.json` (default: `true` when absent)
- [ ] When `enabled: false`, `markban tag` commands show a clear error: "The 'tags' feature is disabled in your config"
- [ ] When `enabled: false`, `create --tags` reports the same error; no `tags` frontmatter field is written by any command
- [ ] When `enabled: false`, `--filter-tag` flag and `--tags` on `create` are absent from their respective help text
- [ ] `markban init` writes `tags: { "enabled": true }` as an explicit default
