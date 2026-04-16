# 12 - Configurable slug casing rules

## Description

Slug generation is currently hardcoded: lowercase, spaces to hyphens, strip non-alphanumeric characters. Allow users to configure the casing style via `markban.json`.

Supported styles:

```json
{
  "slugs": {
    "casing": "kebab"    // default: "kebab" = lowercase-hyphenated
  }
}
```

Options:
- `kebab` -- `my-work-item` (current default)
- `snake` -- `my_work_item`
- `camel` -- `myWorkItem`
- `pascal` -- `MyWorkItem`

Affects `--create`, `--sanitize`, and `--rename`. The configured style is used when deriving a slug from a title. Sanitize should not re-slug files that already match a non-default style -- it should read the config before deciding what "correct" looks like.

---

## Acceptance Criteria

- [x] `slugs.casing` in config drives slug generation in `--create` and `--rename`
- [x] `--sanitize` respects configured casing and does not normalise valid slugs to kebab
- [x] Default `kebab` preserves all existing behaviour
- [x] `markban init` writes `slugs.casing: "kebab"` in the explicit defaults output (see [explicit-defaults-in-init])
- [x] Invalid casing value produces a clear config error
