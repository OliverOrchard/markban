# 22c - Item dependencies via frontmatter

## Description

Allow work items to declare dependencies on other items via a `dependsOn` frontmatter field. This is a unidirectional declaration -- the dependent item names the things it requires, and the inverse ("what does this block?") is computed at query time.

```yaml
---
dependsOn: [lane-role-flags, configurable-lanes]
---
```

Values are slugs, consistent with the existing `[slug]` cross-reference convention already used in item body text.

**Effect on `markban next`:** by default, `markban next` skips items whose `dependsOn` list contains any slug that is not yet in the `done` lane. Only actionable items (all dependencies resolved) surface. `markban next --include-blocked` overrides this.

**Effect on `markban health check-links`:** slug validation is extended to also check `dependsOn` entries -- a missing or misspelled slug is flagged the same way as a broken body ref.

**Effect on `markban list --json`:** `dependsOn` array included in output alongside other frontmatter fields.

**Commands:**
```
markban depends-on <id> <slug>           # add a dependency
markban depends-on <id> --remove <slug>  # remove a dependency
markban depends-on <id>                  # list deps for this item + their status
markban depends-on --list                # all items with at least one unresolved dependency
```

`markban depends-on <id>` shows each slug in `dependsOn`, whether it is in the done lane (resolved) or not, and which lane it is currently in.

`markban depends-on --list` is the board-wide view: all items that cannot be started yet because dependencies are pending.

**No bidirectional sync required.** "What does item X block?" is answered by querying which items have `x-slug` in their `dependsOn`. The `--references` command pattern handles this naturally.

Depends on [frontmatter-layer] (item 21).

---

## Acceptance Criteria

- [ ] `markban depends-on <id> <slug>` adds a slug to the `dependsOn` array (idempotent)
- [ ] `markban depends-on <id> --remove <slug>` removes a slug from `dependsOn`
- [ ] `markban depends-on <id>` lists all dependencies with their current lane and resolved status
- [ ] `markban depends-on --list` lists all items with at least one unresolved dependency
- [ ] `markban next` skips items with unresolved dependencies (dependsOn slugs not in done lane)
- [ ] `markban next --include-blocked` includes dependency-blocked items
- [ ] `markban health check-links` validates slugs in `dependsOn` alongside body refs
- [ ] `markban list --json` includes `dependsOn` array in output
- [ ] `markban references <slug>` includes items that list it in `dependsOn`
- [ ] Circular dependency detection: warn but do not crash
