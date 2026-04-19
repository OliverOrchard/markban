# 28 - Investigate and test duplicate H1 heading scenarios

## Description

Work items were found with two H1 headings -- the correct current heading at the top, plus a stale one from an earlier numbering directly below it. Ten affected files were cleaned up manually. The root cause is unknown: it may be an agent authoring mistake (copying a file and forgetting to remove the old heading), or a bug in a markban command (`rename`, `reorder`, `sanitize`, `create`, or `move`) that writes an extra heading instead of replacing the existing one.

Investigate all commands that write to work item files and determine whether any of them can produce a duplicate H1. Add integration tests covering the scenarios most likely to trigger this, so regressions are caught automatically.

Commands to audit:
- `rename` -- updates H1 and renames file
- `reorder` -- updates H1 on renumbered files
- `sanitize` -- aligns H1 with filename
- `create` -- writes initial H1
- `move` -- should not touch H1, confirm it doesn't

---

## Acceptance Criteria

- [ ] Each of the five commands audited for duplicate-H1 scenarios
- [ ] At least one integration test per command that verifies no duplicate H1 is produced after the command runs
- [ ] Integration test covering `reorder` across multiple renumbering passes (e.g. item renumbered twice)
- [ ] Integration test covering `rename` when the file already has a body with H2/H3 headings
- [ ] Integration test covering `sanitize` on a file that already has a stale second H1
- [ ] If a bug is found in any command, it is fixed and the corresponding test covers the fix
- [ ] If root cause is confirmed to be agent authoring error only, a note is added to the agent workflow instructions

- [ ] Criterion 1
