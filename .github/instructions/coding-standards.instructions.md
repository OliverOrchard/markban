---
applyTo: "Markban.Cli/**/*.cs,Markban.Core/**/*.cs"
---

# Coding Standards

## Method and class size

The guiding heuristic: **if you cannot hold a method or class in working memory at once, it is too large.**

- **Methods ≤ ~20 lines** — extract a well-named helper when growing beyond this.
- **Cyclomatic complexity ≤ 7** per method — each `if`, `else`, `for`, `while`, `switch case`, `catch`, `&&`, `||` costs one unit.
- **One level of abstraction per method** — orchestrate high-level steps OR handle low-level detail; never both in the same method.
- **Command-Query Separation (CQS)** — a method either returns a value (query, no side effects) or changes state (command, returns void). Avoid both.
- **No primitive obsession** — wrap related primitives in a named type (`record`, `struct`, `enum`) rather than passing loose `int`/`string` bundles.
- **Explicit over implicit** — name things clearly; `private`/`internal` on every member; no magic numbers (use a descriptive `const`).
- **Strangler fig for large changes** — add the new path alongside the old, migrate call-sites incrementally, then delete the old path. Avoid big-bang rewrites in a single commit.

## SOLID Principles

| Principle | Applied here |
|---|---|
| **S** — Single Responsibility | Each `*Route` parses args and calls a command. Each `*Command` executes pure domain logic. No class does both. |
| **O** — Open / Closed | Add a new command by adding a new `*Route` + `*Command` pair. Never add branches to `CommandRouter`. |
| **L** — Liskov Substitution | `CommandRoute` subclasses are fully substitutable — `TryRoute` contract must hold. |
| **I** — Interface Segregation | Keep interfaces narrow. `CommandRoute` has one method. Don't grow it. |
| **D** — Dependency Inversion | Routes depend on `WorkItemStore` (static methods today; keep new logic behind interfaces when adding complexity). |

## Patterns in this codebase

The `CommandRoute` / `CommandRouter` shape is the **Strategy pattern** — every route is a strategy. When adding functionality:

- **Strategy** — new command = new `CommandRoute` subclass. Zero changes to `CommandRouter`.
- **Template Method** — if routes share argument-parsing boilerplate, extract a base method, not duplicated code.
- **Chain of Responsibility** — `CommandRouter.Route()` already implements this. Don't bypass it.

When recommending a refactor, name the target pattern explicitly so it is searchable.

## C# / .NET conventions

- **Target framework:** `net10.0`
- **Nullable:** enabled — no `#nullable disable` suppressions.
- **File-scoped namespaces:** `namespace Markban.Core;` (no braces) — enforced as `:warning`.
- **Records for immutable data:** `WorkItem`, `WorkItemSummary` are records — keep them that way.
- **Collection expressions (C# 12):** prefer `[ ]` over `new List<T> { }`.
- **`using` declarations:** prefer `using T x = ...;` (declaration form) over `using (T x = ...)`.
- **`var`:** use when the type is apparent from the right-hand side (`var x = new Foo()`); be explicit for built-in types (`int`, `bool`, `string`).
- **`async void`:** only for event handlers. All other async methods return `Task` or `Task<T>`.
- **One type per file:** file name must match the primary type name.
- **Allman braces:** opening brace on its own line — enforced by `.editorconfig`.
- **Always braces:** every `if`/`else`/`for`/`while` body has braces, even single-liners — enforced as `:warning`.
- **Explicit access modifiers:** `private`/`public`/`internal` on every declaration — enforced as `:warning`.
- **Naming:** `_camelCase` private fields; `PascalCase` constants and static readonlys; `IPascalCase` interfaces.
- **`System.*` usings first**, then alphabetical, no blank lines between groups.
- **`dotnet format`:** run before every commit.

```powershell
# Format everything
dotnet format

# Check only (no writes — useful before committing)
dotnet format --verify-no-changes
```

## CLI design conventions

- One subcommand per concept: `markban block`, not `markban --block`.
- Flags modify behaviour: `markban block <id> --remove`, not a separate `markban unblock`.
- `--list` is a modifier on the subcommand: `markban block --list`.
- `--dry-run` on every command that mutates state.
- Positional args for required inputs; flags for optional modifiers.
- `markban health` groups diagnostic checks: `markban health check-links`, `markban health check-order`.
