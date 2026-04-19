# 25d - Dependency order validation command

## Description

Add a `markban health check-order` command that validates the numeric order of work items against their declared `dependsOn` dependencies. If item 9 depends on item 11 (which comes later in the board), that is a sequencing violation -- item 9 cannot be worked before item 11 is done, but it is positioned as if it will be.

```
markban health check-order
```

Example output:

```
Dependency order violations:
  9-rename-command depends on 11-configurable-slug-casing-rules (9 < 11 but depends on it)
  7-progress-command depends on 4b-lane-role-flags -- OK
  14-parse-boards-config no dependencies -- OK

2 violation(s) found.
```

**How it works:**
1. Loads all ordered items with their numeric IDs
2. For each item, reads `dependsOn` from frontmatter
3. Resolves each slug to its item ID
4. If the dependent item's ID is *lower* than the dependency's ID, it is a violation -- the item is scheduled before the thing it needs

**Cross-lane awareness:** a `dependsOn` slug that resolves to an item in the `done` lane is always satisfied regardless of its original position -- that work is already complete.

**Exit code:** non-zero if any violations are found, so it can be used in CI.

Depends on [item-dependencies-via-frontmatter] (21c).

---

## Acceptance Criteria

- [ ] `markban health check-order` scans all ordered work items for `dependsOn` frontmatter
- [ ] Reports violations where an item's ID is lower than a dependency's ID
- [ ] Skips dependencies already in the `done` lane (already resolved)
- [ ] Reports circular dependencies as a separate class of violation
- [ ] Resolves slugs to IDs using the same lookup as `markban health check-links`
- [ ] Unresolvable slugs in `dependsOn` reported as broken refs (defer to `markban health check-links`)
- [ ] Exit code 0 when no violations, non-zero when violations found
- [ ] `markban health check-order --fix` reorders items to resolve violations where safe (no ambiguity)
- [ ] Works with sub-item IDs (e.g. 4a before 4b is valid; 4b depending on 5 is a violation if 5 > 4)
