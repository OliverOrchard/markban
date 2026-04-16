# Rich markdown rendering for `show` command via Spectre.Console

## Description

The `markban show <id|slug>` command currently outputs raw JSON, which is hard to read for humans -- particularly work items that contain markdown tables, code blocks, and H2 sections.

Add rich terminal rendering using [Spectre.Console](https://spectreconsole.net/) and its `Spectre.Console.Markdown` package. This renders markdown headings, tables, code blocks, bold/italic, and lists with colour and box-drawing characters natively in the terminal.

**Command changes:**

- `markban show <id|slug>` -> renders markdown content as rich terminal output (human view)
- `markban show <id|slug> --json` -> current raw JSON output (machine/pipe view)
- `markban list` and `markban list --summary` -> unchanged (already JSON, used for scripting)

**Prior art:** This is exactly how `gh issue view` (rich) vs `gh issue list --json` (machine) work. The split between human view and machine view is a well-established CLI pattern.

**Dependency:** `Spectre.Console.Markdown` NuGet package (MIT licence, actively maintained, widely used in .NET CLI tooling).

---

## Acceptance Criteria

- [ ] `markban show <id|slug>` renders markdown with headings, tables, code blocks, and lists formatted for the terminal
- [ ] `markban show <id|slug> --json` outputs the existing raw JSON (no regression for scripting)
- [ ] Tables in work item content render as aligned columns with borders
- [ ] Code fences render with syntax highlighting where Spectre supports the language
- [ ] Output degrades gracefully in terminals that do not support colour (respects `NO_COLOR` env var)
- [ ] `Spectre.Console.Markdown` added only to `Markban.Cli` -- not `Markban.Core` (keep core dependency-free)
