# 25 - Frontmatter layer - auto-managed metadata above template body

## Description

Introduce YAML frontmatter as a machine-managed metadata layer at the top of every work item file. This is the foundation for blocked items (19a), tags (19b), due dates, and any future structured metadata.

**File structure with frontmatter:**

```markdown
---
tags: [bug, backend]
blocked: "Waiting on design approval"
---
# 5 - My task

## Description
...
```

Frontmatter is always at the very top of the file. It is auto-managed by markban -- commands read and write specific fields without touching the rest. The H1 heading (if enabled) and template body follow below.

**Frontmatter is opt-in per feature** -- a file with no frontmatter fields is indistinguishable from today's files. No frontmatter block is written unless at least one field has a value. Fully backward compatible.

**Template interaction** -- templates define the body below the H1. Frontmatter is above the H1. Users cannot accidentally overwrite frontmatter via templates. These are two independent layers.

This item covers the core infrastructure: a `FrontmatterParser` that can read and write individual fields without corrupting the rest of the file, used by commands that need structured metadata.

---

## Acceptance Criteria

- [ ] `FrontmatterParser` can read named fields from YAML frontmatter
- [ ] `FrontmatterParser` can write/update named fields without touching other content
- [ ] Files with no frontmatter are read and written identically to today
- [ ] `--sanitize` preserves frontmatter blocks without modification
- [ ] Frontmatter block is only written when at least one field has a value
- [ ] `--list --json` output includes parsed frontmatter fields on each item
