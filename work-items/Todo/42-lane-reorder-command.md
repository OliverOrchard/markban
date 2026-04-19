# 42 - lane reorder command

## Description

Add `markban lane reorder <order>` to change the position of lanes in `markban.json`. Lane order drives the `progress` command (advance one step forward) and the column order in the web UI. Without this command, users must hand-edit the JSON array to reorder lanes.

Related: [lane-rename-command], [lane-add-command], [lane-remove-command]

---

## Acceptance Criteria

- [ ] `markban lane reorder <order>` accepts a comma-separated list of lane names and rewrites the `lanes` array in `markban.json` to that order
- [ ] All currently configured lane names must be present in `<order>` — missing or unknown names produce an error and nothing is changed
- [ ] The reordered config preserves all lane properties (`ordered`, `type`, `pickable`, `wip`, `defaultCollapsed`) — only position changes
- [ ] `--dry-run` prints the new lane order without modifying anything
- [ ] The web UI column order reflects the new lane order after a page reload
- [ ] The `progress` command advances items in the new order after a reorder
- [ ] Unit test: lanes reordered correctly in config
- [ ] Unit test: error when a lane name is missing from the order argument
- [ ] Unit test: error when an unknown lane name is in the order argument
- [ ] Unit test: dry-run produces no side effects
