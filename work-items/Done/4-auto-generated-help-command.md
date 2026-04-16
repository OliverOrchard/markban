# 4 - Auto-generated help command

## Description

Replace the hand-maintained `HelpCommand.cs` string table with help text generated from the command definitions themselves. The current implementation is a list of `Console.WriteLine` calls that goes stale every time a command is added, renamed, or changed.

Each command should declare its own help entry -- usage line, flags, and description -- close to the code that implements it. `HelpCommand` then collects and renders these at runtime. This ensures help is always accurate without a separate maintenance step.

**Implementation options:**

1. **Attribute-based** -- `[CliCommand("--progress", "<id|slug>", "Advance item one lane forward")]` on each command class. Help reflects on these at startup.
2. **Registration-based** -- each command registers a `HelpEntry` record in a shared collection during `CommandRouter` setup. No reflection needed, explicit and testable.
3. **Source-generated** -- build-time generation from a structured definition file. Most complex, probably overkill.

Option 2 is recommended: explicit, no magic, easy to test that all registered commands have help entries.

**Format stays the same** -- columnar layout matching the current output. No UX regression.

This also makes it trivial to add `--help <command>` for per-command detailed help in future.

---

## Acceptance Criteria

- [x] All existing commands have a registered help entry with usage line and description
- [x] `--help` output is identical in content to the current hand-written output
- [x] Adding a new command without a help entry causes a test to fail (coverage check)
- [x] `HelpCommand.cs` contains no hardcoded usage strings
- [x] `--help <command>` parses but can fall back to full help for now (foundation laid)
