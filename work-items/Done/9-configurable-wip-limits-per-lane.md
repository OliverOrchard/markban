# 9 - Configurable WIP limits per lane

## Description

Allow each lane to define an optional `wip` -- a maximum number of items that can be in that lane at once. Any command that moves an item into a lane (`--move`, `--create`, `--start`) checks the limit before proceeding and errors if it would be exceeded. A `--override-wip` flag bypasses the check for exceptional cases.

This enables proper kanban WIP discipline without any external tooling.

Config:

```json
{
  "lanes": [
    { "name": "Todo",        "ordered": true,  "type": "ready" },
    { "name": "In Progress", "ordered": true,  "wip": 2 },
    { "name": "Testing",     "ordered": true,  "wip": 3 },
    { "name": "Done",        "ordered": true,  "type": "done" },
    { "name": "Ideas",       "ordered": false, "pickable": false },
    { "name": "Rejected",    "ordered": false, "pickable": false }
  ]
}
```

No `wip` property means no restriction -- default behaviour, fully backward compatible.

**Error message** should be informative:
```
Error: 'In Progress' is at its WIP limit (2/2).
Use --override-wip to proceed anyway, or move an item out first.
```

The web UI should visually indicate lanes at or near their limit (e.g. column header turns amber at 80%, red at 100%).

---

## Acceptance Criteria

- [x] `wip` property on a lane config enforces a max item count for that lane
- [x] `--move`, `--create --lane`, and `--start` all check the limit before moving
- [x] Clear error message shows current count, limit, and how to override
- [x] `--override-wip` flag bypasses the check on any command
- [x] No `wip` set = no restriction, fully backward compatible
- [x] Web UI column header reflects limit status visually (split out to [web-ui-wip-limit-indicator])
- [x] `markban init` writes lanes without `wip` (users opt in explicitly)
- [x] Unit tests for at-limit and over-limit scenarios
