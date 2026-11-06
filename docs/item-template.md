# Custom Item Template

By default, `markban create` generates new work items with a `## Description` and `## Acceptance Criteria` section (below the `# {id} - Title` H1 heading when `heading.enabled` is `true`).

To use your own template, add a `.template.md` file inside your `work-items/` directory (or the board directory configured by `rootPath` in [`markban.json`](configuration.md)):

```markdown
## Context

Why this work matters.

## Tasks

- [ ] Task 1
- [ ] Task 2

## Acceptance Criteria

- [ ] Criterion 1
```

The contents of `work-items/.template.md` become the body of every new item created with `markban create` (after any configured YAML frontmatter and the auto-generated `# {id} - Title` heading). Sub-items created with `markban create "Title" --sub-item --parent <id>` use the same `.template.md` file.
