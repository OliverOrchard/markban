# 37 - Fix root board always visible when boards array is configured

## Description

When a user adds a `boards` array to `markban.json` but does not explicitly include their own root board, that board silently disappears from the web UI and `markban overview`. This is a confusing footgun: the user expects the board they have been working in to still be visible.

`LoadBoards` should automatically prepend the root board (resolved from `rootPath` in the same config) as the first entry whenever a `boards` array is present and the root board is not already in it. The display name should default to the directory name of the board root (capitalised), or a configurable `"name"` at the root level of the config.

---

## Acceptance Criteria

- [ ] When `boards` is present and does not include the root board path, `LoadBoards` prepends the root board automatically
- [ ] When `boards` is present and already includes the root board, it is not duplicated
- [ ] The auto-inserted root board entry has a sensible display name (directory name, capitalised)
- [ ] A top-level `"name"` key in `markban.json` overrides the auto-derived display name for the root board
- [ ] `markban overview` is consistent with the web UI -- both show the root board
- [ ] When no `boards` key is present, behaviour is unchanged (root board is the only board, single-board mode)
- [ ] Unit test: root board prepended when absent from array
- [ ] Unit test: root board not duplicated when already in array
- [ ] Unit test: custom name respected
- [ ] Integration test: web `/api/boards` returns root board as first entry when array omits it
