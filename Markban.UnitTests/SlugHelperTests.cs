using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class SlugHelperTests
{
    // -------------------------------------------------------------------------
    // Generate — kebab (default)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("My Work Item", "my-work-item")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("Alpha Task", "alpha-task")]
    public void Generate_Kebab_ConvertsSpacesToHyphens(string title, string expected)
    {
        // Act
        var slug = SlugHelper.Generate(title, "kebab");

        // Assert
        slug.Should().Be(expected);
    }

    [Fact]
    public void Generate_Kebab_TreatsHyphenInTitleAsWordBoundary()
    {
        // Arrange / Act
        var slug = SlugHelper.Generate("Feature-X Setup", "kebab");

        // Assert
        slug.Should().Be("feature-x-setup");
    }

    [Fact]
    public void Generate_NoExplicitCasing_DefaultsToKebab()
    {
        // Act
        var slug = SlugHelper.Generate("My Item");

        // Assert
        slug.Should().Be("my-item");
    }

    // -------------------------------------------------------------------------
    // Generate — snake
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("My Work Item", "my_work_item")]
    [InlineData("Hello World", "hello_world")]
    public void Generate_Snake_ConvertsSpacesToUnderscores(string title, string expected)
    {
        // Act
        var slug = SlugHelper.Generate(title, "snake");

        // Assert
        slug.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Generate — camel
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("my work item", "myWorkItem")]
    [InlineData("hello world", "helloWorld")]
    [InlineData("alpha", "alpha")]
    public void Generate_Camel_CapitalizesWordsAfterFirst(string title, string expected)
    {
        // Act
        var slug = SlugHelper.Generate(title, "camel");

        // Assert
        slug.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Generate — pascal
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("my work item", "MyWorkItem")]
    [InlineData("hello world", "HelloWorld")]
    [InlineData("alpha", "Alpha")]
    public void Generate_Pascal_CapitalizesAllWords(string title, string expected)
    {
        // Act
        var slug = SlugHelper.Generate(title, "pascal");

        // Assert
        slug.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // IsValidCasing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("kebab")]
    [InlineData("snake")]
    [InlineData("camel")]
    [InlineData("pascal")]
    [InlineData("KEBAB")]
    [InlineData("Snake")]
    public void IsValidCasing_KnownValues_ReturnsTrue(string casing)
    {
        // Act / Assert
        SlugHelper.IsValidCasing(casing).Should().BeTrue();
    }

    [Theory]
    [InlineData("dash")]
    [InlineData("lower")]
    [InlineData("")]
    [InlineData("title-case")]
    public void IsValidCasing_UnknownValues_ReturnsFalse(string casing)
    {
        // Act / Assert
        SlugHelper.IsValidCasing(casing).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Backward compatibility: kebab matches original regex behaviour
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Auto-generated Help Command", "auto-generated-help-command")]
    [InlineData("Configurable WIP Limits", "configurable-wip-limits")]
    [InlineData("  Trimmed  Title  ", "trimmed-title")]
    public void Generate_Kebab_MatchesOriginalBehaviour(string title, string expected)
    {
        // The original slug generation was:
        // Regex.Replace(title.ToLower().Trim(), @"[^a-z0-9\s-]", "").Replace(" ", "-")
        // SlugHelper generates equivalent output for standard titles.

        // Act
        var slug = SlugHelper.Generate(title, "kebab");

        // Assert
        slug.Should().Be(expected);
    }
}
