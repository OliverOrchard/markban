# 22a - Blocked items via frontmatter

# 19a - Blocked items

## Description

Allow work items to be marked as blocked with an optional reason, stored in frontmatter. Blocked status is a state overlay -- it does not change the item's lane.

Commands:
```
markban block <id> "Waiting on API keys from ops"   # mark as blocked
markban block <id> --remove                          # unblock
markban block --list                                 # all blocked items across lanes
```

`markban block --list` shows all currently blocked items across all lanes, including the reason. Useful for a standup check or a quick triage of what is stuck.

This writes/clears `blocked` in the file's frontmatter:
```yaml
---
blocked: "Waiting on API keys from ops"
---
```

**Web UI:** Blocked items get a visual indicator on their card (red border or badge showing the reason on hover). Blocked count shown in lane header.

**CLI:** `markban overview` shows blocked items count. `markban list` output includes `blocked` field. `markban next` skips blocked items by default (they're not actionable); `markban next --include-blocked` to override.

Depends on [frontmatter-layer] (frontmatter layer).

---

## Acceptance Criteria

- [ ] `markban block <id> "reason"` writes `blocked` field to frontmatter
- [ ] `markban block <id> --remove` clears `blocked` field from frontmatter
- [ ] `markban block --list` lists all blocked items across lanes with their reasons
- [ ] `markban next` skips blocked items; `markban next --include-blocked` includes them
- [ ] `markban overview` shows blocked item count per lane
- [ ] Web UI card shows visual blocked indicator with reason on hover
- [ ] `markban list --json` includes `blocked` field in output
- [ ] Feature is gated by `blocked: { "enabled": true }` in `markban.json` (default: `true` when absent)
- [ ] When `enabled: false`, `markban block` commands show a clear error: "The 'blocked' feature is disabled in your config"
- [ ] When `enabled: false`, no `blocked` frontmatter field is written by any command (including `create`)
- [ ] When `enabled: false`, `markban help block` is absent from command listing
- [ ] `markban init` writes `blocked: { "enabled": true }` as an explicit default
