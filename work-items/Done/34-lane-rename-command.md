# 34 - lane rename command

## Description

Add `markban lane rename <old-name> <new-name>` to atomically rename a lane: rename the directory on disk and update the `name` field in `markban.json`. Without this command users must do three manual steps (rename folder, edit JSON, verify nothing is broken) and risk leaving the board in an inconsistent state.

Related: [lane-add-command], [lane-remove-command]

---

## Acceptance Criteria

- [x] `markban lane rename <old> <new>` renames the lane directory from `<old>` to `<new>`
- [x] The `name` field of the matching lane entry in `markban.json` is updated to `<new>`
- [x] If `<old>` is not a configured lane, an error is printed and nothing is changed
- [x] If a directory named `<new>` already exists, an error is printed and nothing is changed
- [x] The rename is atomic: if the JSON write fails, the directory rename is rolled back (or vice versa)
- [x] `--dry-run` flag prints what would change without modifying anything
- [x] The command works regardless of which directory the user is in (board root is discovered via `FindRoot`)
- [x] Unit test: rename succeeds and both directory and config reflect the new name
- [x] Unit test: error when old lane does not exist
- [x] Unit test: error when target name already exists
- [x] Unit test: dry-run produces no side effects
