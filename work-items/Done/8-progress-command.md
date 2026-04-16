# 8 - Progress command

## Description

Add a `--progress <id|slug>` command that advances a work item one lane forward in config array order. This is the structured way to move items through the workflow without specifying an explicit target lane.

The command skips lanes that have `pickable: false` (e.g. `Ideas`, `Rejected`) when determining the next lane. It stops at the lane with `type: "done"` and informs the user if the item is already there.

This is the underlying move behaviour also used by `--start` (when feature branches are disabled, `--start` is `--progress` + optional branch creation). Having a dedicated command makes the workflow explicit and composable from the CLI.

**Examples:**

```
markban progress 5
  -> Moves item 5 from Todo -> In Progress

markban progress 5  (if already at Done)
  -> "Item 5 is already in the done lane."
```

**`progress` vs `move`:**
- `markban progress` always moves forward one lane (safe, sequential)
- `markban move` is the explicit escape hatch for jumping to any lane (including backwards)

---

## Acceptance Criteria

- [x] `markban progress <id|slug>` moves item to the next lane in config array order
- [x] Array order in `markban.json` determines the forward direction
- [x] Skips lanes with `pickable: false` when finding the next lane
- [x] Stops at `type: "done"` lane and prints informational message rather than erroring
- [x] Errors clearly if item identifier is not found
- [x] Works with both numeric ID and slug
- [x] `markban progress <id> --dry-run` shows planned move without executing
- [x] Depends on [lane-role-flags] (type/pickable wired into commands)
- [x] Integration test: progress through a full workflow from ready -> done
