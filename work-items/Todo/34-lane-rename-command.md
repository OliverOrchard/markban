# 34 - lane rename command

## Description

Add `markban lane rename <old-name> <new-name>` to atomically rename a lane: rename the directory on disk and update the `name` field in `markban.json`. Without this command users must do three manual steps (rename folder, edit JSON, verify nothing is broken) and risk leaving the board in an inconsistent state.

Related: [lane-add-command], [lane-remove-command]

---

## Acceptance Criteria

- [ ] `markban lane rename <old> <new>` renames the lane directory from `<old>` to `<new>`
- [ ] The `name` field of the matching lane entry in `markban.json` is updated to `<new>`
- [ ] If `<old>` is not a configured lane, an error is printed and nothing is changed
- [ ] If a directory named `<new>` already exists, an error is printed and nothing is changed
- [ ] The rename is atomic: if the JSON write fails, the directory rename is rolled back (or vice versa)
- [ ] `--dry-run` flag prints what would change without modifying anything
- [ ] The command works regardless of which directory the user is in (board root is discovered via `FindRoot`)
- [ ] Unit test: rename succeeds and both directory and config reflect the new name
- [ ] Unit test: error when old lane does not exist
- [ ] Unit test: error when target name already exists
- [ ] Unit test: dry-run produces no side effects
