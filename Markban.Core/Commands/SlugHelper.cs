using System.Text.RegularExpressions;

public static class SlugHelper
{
    private static readonly HashSet<string> ValidCasings =
        new(StringComparer.OrdinalIgnoreCase) { "kebab", "snake", "camel", "pascal" };

    public static bool IsValidCasing(string casing) => ValidCasings.Contains(casing);

    /// <summary>
    /// Generates a slug from a title using the specified casing style.
    /// Strips non-alphanumeric characters (hyphens and underscores become word boundaries).
    /// </summary>
    public static string Generate(string title, string casing = "kebab")
    {
        var lower = title.Trim().ToLower();
        var clean = Regex.Replace(lower, @"[^a-z0-9\s\-_]", "");
        var words = clean.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        return casing.ToLower() switch
        {
            "snake" => string.Join("_", words),
            "camel" => words[0] + string.Concat(words.Skip(1).Select(Capitalize)),
            "pascal" => string.Concat(words.Select(Capitalize)),
            _ => string.Join("-", words),
        };
    }

    private static string Capitalize(string w) =>
        w.Length == 0 ? "" : char.ToUpper(w[0]) + w[1..];
}
