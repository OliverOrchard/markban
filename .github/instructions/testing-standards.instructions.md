---
applyTo: "Markban.UnitTests/**/*.cs,Markban.IntegrationTests/**/*.cs"
---

# Testing Standards

Framework: **xUnit + AwesomeAssertions** (`net10.0`).

## Rules

1. **Arrange / Act / Assert comments — always.** Every test method has `// Arrange`, `// Act`, `// Assert` markers on their own lines. If there is trivially no arrange, use `// Arrange — <reason>` on one line and combine Act + Assert.

2. **Use AwesomeAssertions — never `Assert.*`.** Add `using AwesomeAssertions;`. Use `.Should()` chains everywhere.

| xUnit | AwesomeAssertions |
|---|---|
| `Assert.Equal(expected, actual)` | `actual.Should().Be(expected)` |
| `Assert.True(x)` | `x.Should().BeTrue()` |
| `Assert.Null(x)` | `x.Should().BeNull()` |
| `Assert.Empty(col)` | `col.Should().BeEmpty()` |
| `Assert.Single(col)` | `col.Should().ContainSingle()` |

Add a `because:` string to assertions where the failure message would not be self-evident.

3. **Integration tests** live in `Markban.IntegrationTests/` and run the real CLI binary end-to-end via `CliRunner`. Follow the existing fixture pattern (`ToolBuildFixture`, `TestWorkspace`).

## Running tests

```powershell
dotnet test                                              # all test projects
dotnet test Markban.UnitTests/                          # unit tests only
dotnet test Markban.IntegrationTests/                  # integration tests only
```
