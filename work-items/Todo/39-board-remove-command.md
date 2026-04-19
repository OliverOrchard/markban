# 39 - board remove command

## Description

Add `markban board remove <key>` to unregister a board from the current root config's `boards` array. This removes the entry from `markban.json` but does **not** delete the board directory or its contents -- it only unlinks the board from the multi-board view.

Related: [board-add-command]

---

## Acceptance Criteria

- [ ] `markban board remove <key>` removes the matching entry from the `boards` array in `markban.json`
- [ ] The board directory and all its files are left untouched -- only the config entry is removed
- [ ] Attempting to remove the root board (the board whose `rootPath` is this config's working board) prints an error and exits -- the root board cannot be unlinked from itself
- [ ] If `<key>` does not match any entry in the `boards` array, an error is printed and nothing is changed
- [ ] If removing the entry leaves the `boards` array empty, the `boards` key is removed from the config entirely (returning to single-board mode)
- [ ] `--dry-run` prints what would change without modifying anything
- [ ] Unit test: entry removed from config; directory unchanged
- [ ] Unit test: error when attempting to remove the root board
- [ ] Unit test: error when key not found
- [ ] Unit test: `boards` key removed when array becomes empty
- [ ] Unit test: dry-run produces no side effects
