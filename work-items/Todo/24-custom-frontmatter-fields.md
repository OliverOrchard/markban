# 24 - Custom frontmatter fields

Depends on [frontmatter-layer]

## Description

Allow users to define their own frontmatter fields that are automatically written into every new work item, and to inject ad-hoc fields at create time via `--set`. This is the user-facing layer of the frontmatter infrastructure introduced in [frontmatter-layer].

### Config-driven defaults

A `customFrontmatter` array in `markban.json` defines fields that are written into every new item's frontmatter. Each entry is an object with a `name` and a `default` value:

```json
"customFrontmatter": [
  { "name": "assigned",  "default": "" },
  { "name": "estimate",  "default": null },
  { "name": "epic",      "default": "" },
  { "name": "priority",  "default": "medium" }
]
```

A new item created on this board will open with:

```markdown
---
assigned: ""
estimate: null
epic: ""
priority: "medium"
---

# 24 - My Task
```

When `customFrontmatter` is absent or empty, no custom fields are written. Fully backward compatible.

### `create --set key=value`

Users can inject or override frontmatter values at create time without touching config:

```
markban create "Add retry logic" --set epic=payments --set assigned=alice --set estimate=3
```

Config defaults are written first; `--set` values are merged on top (overriding any matching default). `--set` is repeatable. Values are treated as strings unless they match `null`, `true`, or `false` exactly (which are written as YAML primitives).

### Command transparency

All other commands (`move`, `rename`, `reorder`, `sanitize`, `health`) must pass through unknown frontmatter keys untouched — they read and write only the fields they own.

### `init` integration

`markban init` writes `customFrontmatter: []` as an explicit default in the generated config, so users can see the extension point and fill it in.

### Design notes for future native feature toggles

Native markban frontmatter features (`blocked`, `dependsOn`, `tags`) will each be implemented as top-level config objects with an `enabled` flag, following the same pattern as `heading`:

```json
"blocked":   { "enabled": true },
"dependsOn": { "enabled": true },
"tags":      { "enabled": true }
```

These are out of scope for this item but should not conflict with `customFrontmatter`. The `customFrontmatter` key name is reserved; users cannot name a custom field `customFrontmatter`.

---

## Acceptance Criteria

- [ ] `customFrontmatter` config section parsed from `markban.json` as an array of `{ name, default }` objects
- [ ] `create` writes custom fields into frontmatter in config-defined order when `customFrontmatter` is non-empty
- [ ] `create --set key=value` injects frontmatter values at create time, merging over config defaults
- [ ] `--set` is repeatable; all supplied key/value pairs are written
- [ ] `--set` values `null`, `true`, `false` are written as YAML primitives, not quoted strings
- [ ] `--set` with no matching config default still writes the field (purely ad-hoc)
- [ ] When `customFrontmatter` is absent or empty and no `--set` flags given, no frontmatter block is written (backward compatible)
- [ ] `move`, `rename`, `reorder`, `sanitize` and `health` all preserve unknown frontmatter keys untouched
- [ ] `markban init` writes `customFrontmatter: []` in the generated config
- [ ] `markban init --dry-run` includes `customFrontmatter: []` in the preview output
- [ ] Using a reserved name (`customFrontmatter`) as a custom field name gives a clear error

- [ ] Criterion 1
