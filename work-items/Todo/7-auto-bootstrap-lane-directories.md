# 7 - Auto-bootstrap lane directories

## Description

Any command that depends on a board root should silently create missing standard lane directories (`Todo/`, `In Progress/`, `Testing/`, `Done/`, `ideas/`, `Rejected/`) rather than crashing with a `DirectoryNotFoundException`. This removes the current requirement to manually create directories before first use -- discovered when creating an item in the `ideas` lane that had never been used before.

Behaviour:
- Creates only the directories that are missing -- never overwrites or clears existing ones
- Silent -- no output when directories are created, so CLI output stays clean for scripting
- Applies to all commands that write to the board (`--create`, `--move`, `--reorder`, etc.)
- Does NOT implicitly create the board root itself -- that still requires `markban init` or a pre-existing `work-items/` dir

---

## Acceptance Criteria

- [ ] `markban --create "Title" --lane ideas` succeeds even if `ideas/` does not exist
- [ ] `markban --move <id> Done` succeeds even if `Done/` does not exist
- [ ] No output is produced when directories are auto-created
- [ ] Existing directories and their contents are untouched
- [ ] Unit test covering the missing-lane scenario
