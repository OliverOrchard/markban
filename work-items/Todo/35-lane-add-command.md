# 35 - lane add command

## Description

Add `markban lane add <name>` to create a new lane: appends a well-formed entry to the `lanes` array in `markban.json` and immediately creates the corresponding directory. This is a single atomic operation -- the board is ready to use straight away without needing to re-run `markban init`.

Related: [lane-rename-command], [lane-remove-command]

---

## Acceptance Criteria

- [ ] `markban lane add <name>` appends a new lane entry to `markban.json` with sensible defaults (`ordered: true`, no special type, `pickable: true`)
- [ ] The lane directory is created immediately (no need to re-run `markban init`)
- [ ] Optional flags to set lane properties: `--unordered`, `--type <ready|done>`, `--not-pickable`
- [ ] If a lane with the same name already exists in config, an error is printed and nothing is changed
- [ ] If the directory already exists but the lane is not in config, the directory is reused and the entry is appended
- [ ] Only one `type: ready` and one `type: done` lane may exist -- attempting to add a duplicate type without removing the existing one prints an error
- [ ] `--dry-run` flag prints what would change without modifying anything
- [ ] Unit test: new lane entry appears in config and directory is created
- [ ] Unit test: error on duplicate lane name
- [ ] Unit test: error on duplicate type assignment
- [ ] Unit test: dry-run produces no side effects
