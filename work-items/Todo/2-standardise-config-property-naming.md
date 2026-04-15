# 2 - Standardise config property naming

## Description

Before any configurable lane flags or new config keys ship, agree and document the complete `markban.json` property naming conventions so all features use a consistent schema.

**Lane properties:**

| Property | Type | Purpose |
|---|---|---|
| `name` | string | Lane directory name |
| `ordered` | boolean | `true` = numbered filenames (1-slug.md); `false` = slug-only |
| `type` | string enum | `"ready"` = commitment lane; `"done"` = terminal lane; omit for neutral lanes |
| `pickable` | boolean | `false` = excluded from `--next` and `--create` default; defaults to `true` |
| `wip` | number | Max items in lane (see [configurable-wip-limits-per-lane]) |

Using a `type` enum keeps lane roles mutually exclusive by design -- no god lane problem, no conflicting booleans. Lane array order drives the `--progress` command.

**Git config renames (before [feature-branch-workflow-mode] ships):**

| Current | Standardised | Reason |
|---|---|---|
| `pullBeforeStart` | `pullOnStart` | Shorter, reads as an event |
| `checkoutMainOnDone` | `checkoutOnDone` | Shorter, `main` implied by `mainBranch` config |

**Naming conventions:**
- camelCase throughout
- No `is` prefix on booleans in JSON config (that is a C# convention)
- Enum string values are lowercase

**CLI command design conventions (applies to all new commands shipping after item 1):**
- Subcommands for all verbs: `markban block`, `markban tag`, `markban depends-on`
- `--list` as a modifier on the subcommand: `markban block --list`
- `--remove` as a modifier rather than a separate command where it reads naturally
- Positional args for required inputs, named flags for optional modifiers
- `--dry-run` on any command that mutates state

This must land before items 5, 5a, 5b, 21 ship.

---

## Acceptance Criteria

- [ ] `markban.json` schema documented with all reserved lane properties and their types
- [ ] `markban init` output uses all standardised names
- [ ] `type` enum values are `"ready"` and `"done"` -- no other role types at this stage
- [ ] `pickable: false` is the opt-out mechanism for non-workflow lanes
- [ ] `wip` is the property name for WIP limits
- [ ] Git config uses `pullOnStart` and `checkoutOnDone`
- [ ] Documentation and help text consistent throughout
- [ ] Items 5, 5b, 21 updated to reference final schema names
