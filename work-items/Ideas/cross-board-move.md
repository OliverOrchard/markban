# Cross-board move

## Description

Allow `--move` to target a different board as the destination, not just a different lane within the same board. For example: `markban --move 5 Done --board frontend`.

This is non-trivial due to several open design questions:

- **ID conflicts** -- both boards may have a `5-something.md`. Moving a numbered item to a board that already has that ID requires renumbering on arrival.
- **Which config wins** -- if the destination board has its own `markban.json`, should its settings (e.g. custom lane names) apply to the moved item?
- **Atomicity** -- move should be all-or-nothing; partial failure (file copied but not deleted) must be handled.

Park until multiboard web UI (items [multiboard-web-ui---boards-array-in-markbanjson-and-select-box-switcher]-[web-ui-board-switcher-select-box-in-top-right-corner]) and init ([init-command---scaffold-markbanjson-and-board-directories-with-optional-custom-path]) are shipped and dogfooded. Real usage will clarify whether cross-board move is actually needed or if copying manually + `--create` is sufficient.

---

## Notes

- `--root` already allows targeting any board from the CLI for single-board operations
- Consider whether this is better served by an export/import pair rather than a single move command
