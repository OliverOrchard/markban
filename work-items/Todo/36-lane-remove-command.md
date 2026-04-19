# 36 - lane remove command

## Description

Add `markban lane remove <name>` to remove a lane: deletes the entry from `markban.json` and removes the directory. Safety guards prevent data loss and protect required lanes.

Related: [lane-rename-command], [lane-add-command]

---

## Acceptance Criteria

- [ ] `markban lane remove <name>` removes the lane entry from `markban.json` and deletes the directory
- [ ] If the lane directory contains any `.md` files, the command refuses and prints an error -- the user must move or delete items first
- [ ] If the lane has `type: ready` or `type: done`, the command refuses unless `--force` is passed, with a clear warning that a required role is being removed
- [ ] If `<name>` is not a configured lane, an error is printed and nothing is changed
- [ ] `--dry-run` flag prints what would change (files that would be affected, JSON diff) without modifying anything
- [ ] Unit test: remove succeeds when lane is empty
- [ ] Unit test: error when lane contains items
- [ ] Unit test: error when removing a `type: ready` or `type: done` lane without `--force`
- [ ] Unit test: succeeds with `--force` on a typed lane
- [ ] Unit test: dry-run produces no side effects
