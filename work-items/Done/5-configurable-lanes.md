# 5 - Configurable lanes

## Description

Allow users to define their own lanes in `markban.json`. Each lane has a name (= the directory name), an `ordered` flag, and optionally a position in the board. Ordered lanes use numbered filenames (`1-slug.md`) and participate in priority sorting and `--reorder`. Unordered lanes use slug-only filenames (`slug.md`) like the current `ideas/` and `Rejected/` lanes.

This makes the lane model explicit and extensible -- a team could have `Blocked` or `On Hold` lanes, or rename `In Progress` to `Doing`, without any code changes.

Each lane has an optional `type` and `pickable` flag on top of `ordered`:

- `type: "ready"` -- the commitment lane. `--create` defaults here and `--next` reads from here. Only one lane should have this type.
- `type: "done"` -- the terminal lane. `--commit` moves items here, sorted descending. Only one lane should have this type.
- `pickable: false` -- excludes a lane from the pick chain. Apply to `Ideas`, `Rejected`, or any holding lane. Defaults to `true` if omitted.

Array order in the config drives the `--progress` command (advance an item one lane forward, stopping at the `done` lane).

Config shape:

```json
{
  "lanes": [
    { "name": "Backlog",     "ordered": true },
    { "name": "Todo",        "ordered": true,  "type": "ready" },
    { "name": "In Progress", "ordered": true },
    { "name": "Done",        "ordered": true,  "type": "done" },
    { "name": "Ideas",       "ordered": false, "pickable": false },
    { "name": "Rejected",    "ordered": false, "pickable": false }
  ]
}
```

When no `lanes` key is present, the hardcoded defaults apply unchanged -- fully backward compatible.

`WorkItemStore.LoadAll` reads the lane list from config (or falls back to defaults) and iterates those directories. `MoveCommand`, `CreateCommand`, `ReorderCommand`, `CommitCommand`, and `ListCommand` (`--next`) all resolve lane references via config rather than hardcoded strings.

---

## Acceptance Criteria

- [x] `lanes` array in `markban.json` drives which directories are scanned and in what order
- [x] `ordered: true` lanes use numbered filenames and support `--reorder`
- [x] `ordered: false` lanes use slug-only filenames, no numbering
- [x] `type: "done"` lane is the target for `--commit` -- no hardcoded `"Done"`
- [x] `type: "ready"` lane is the default for `--create` -- no hardcoded `"Todo"`
- [x] `type: "ready"` lane is the source for `--next` -- no hardcoded `"Todo"`
- [x] Validation errors if zero or multiple lanes have `type: "done"`
- [x] `--move` validates target against configured lanes, not hardcoded list
- [x] `--create --lane` validates against configured lanes
- [x] Missing `lanes` key falls back to current hardcoded defaults -- no breaking change
- [x] Web UI columns reflect configured lanes
- [x] See also [explicit-defaults-in-init] -- `markban init` writes the full lane config so users can see and edit it
