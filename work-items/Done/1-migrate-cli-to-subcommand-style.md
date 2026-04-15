# 1 - Migrate CLI to subcommand style

## Description

Migrate all CLI commands from the current `--flag` verb style to a `subcommand` style, consistent with modern CLI tools (git, dotnet, npm, gh). This must land before any new commands are added so the migration is a single clean sweep rather than an ongoing split.

**Current style -> subcommand style:**

| Current | Subcommand |
|---|---|
| `markban --list` | `markban list` |
| `markban --create "Title"` | `markban create "Title"` |
| `markban --move <id> <lane>` | `markban move <id> <lane>` |
| `markban --commit <id> --tag <tag>` | `markban commit <id> --tag <tag>` |
| `markban --next` | `markban next` |
| `markban --next-id` | `markban next-id` |
| `markban --reorder <lane> <order>` | `markban reorder <lane> <order>` |
| `markban --overview` | `markban overview` |
| `markban --sanitize` | `markban sanitize` |
| `markban --check-links` | `markban health check-links` |
| `markban --references <slug>` | `markban references <slug>` |
| `markban --git-history <file>` | `markban git-history <file>` |
| `markban --search <term>` | `markban search <term>` |
| `markban --id <id>` / `--slug <slug>` | `markban show <id\|slug>` |
| `markban web` | `markban web` (already correct) |
| `markban --help` | `markban help` or `markban --help` (both) |

**`markban health` -- grouped board diagnostics:**

`markban health` with no argument runs all checks in sequence and prints a combined report. Individual checks can be run in isolation:

```
markban health                   # run all checks
markban health check-links
markban health check-links --include-ideas
markban health check-order
markban health check-order --fix
```

This makes `markban health` suitable as a single CI gate command. `sanitize`, `references`, and `git-history` stay at the top level -- they are utilities/queries, not diagnostic checks.

**Global flags** (apply to all subcommands, come before the subcommand):
- `--root <path>` -- override board root
- `--dry-run` -- where supported per command

**Backward compatibility:** None -- clean break. Pre-v1.0 with few adopters; old `--flag` forms are simply removed.

**CLI design conventions (to be followed by all future commands):**
- One subcommand per concept -- `markban block`, not `markban --block`
- Flags modify behaviour -- `markban block <id> --remove`, not a separate `markban unblock`
  - Exception: `--remove` applied when the flag form would read awkwardly (e.g. `unblock` reads clearly enough to keep)
- `--list` is a modifier on the subcommand, not a separate command -- `markban block --list`
- `--dry-run` available on any command that mutates state
- Positional args for required inputs, flags for optional modifiers

---

## Acceptance Criteria

- [x] All existing commands available as subcommands (table above)
- [x] `markban health` runs all checks and exits non-zero if any fail
- [x] `markban health check-links` and `markban health check-order` work as individual checks
- [x] `markban show <id|slug>` replaces `--id` and `--slug`
- [x] `--root` works as a global flag before any subcommand
- [x] Old `--flag` forms are removed entirely (clean break -- pre-v1.0)
- [x] `markban help` and `markban --help` both show help
- [x] All integration and unit tests updated to use subcommand style
- [x] README updated with subcommand syntax
- [x] Depends on [auto-generated-help-command] (item 4) -- help system is refactored at the same time so new subcommands register their own help entries
