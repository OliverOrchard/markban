using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Reads and writes YAML frontmatter at the top of markdown files.
/// Supports string, list, bool, and null field values.
/// Frontmatter is auto-managed; unknown fields are preserved verbatim.
/// </summary>
public static class FrontmatterParser
{
    private const string Delimiter = "---";

    /// <summary>Returns true if content begins with a frontmatter block.</summary>
    public static bool HasFrontmatter(string content)
        => content.StartsWith(Delimiter + "\n") || content.StartsWith(Delimiter + "\r\n");

    /// <summary>
    /// Splits content into frontmatter fields and the body that follows.
    /// Returns an empty dictionary and the full content as body when no frontmatter is present.
    /// </summary>
    public static (Dictionary<string, object?> Fields, string Body) Parse(string content)
    {
        if (!HasFrontmatter(content))
        {
            return ([], content);
        }

        var end = FindFrontmatterEnd(content);
        if (end < 0)
        {
            return ([], content);
        }

        var yamlBlock = content[Delimiter.Length..end].TrimStart('\r', '\n');
        var body = content[(end + Delimiter.Length)..].TrimStart('\r', '\n');

        return (ParseYaml(yamlBlock), body);
    }

    /// <summary>Reads a string field from the frontmatter. Returns null when absent.</summary>
    public static string? GetField(string content, string key)
    {
        var (fields, _) = Parse(content);
        return fields.TryGetValue(key, out var val) ? val?.ToString() : null;
    }

    /// <summary>Reads a list field from the frontmatter. Returns null when absent.</summary>
    public static List<string>? GetListField(string content, string key)
    {
        var (fields, _) = Parse(content);
        if (!fields.TryGetValue(key, out var val))
        {
            return null;
        }

        return val is List<string> list ? list : null;
    }

    /// <summary>
    /// Writes or updates a single field, preserving all other content.
    /// Pass null to remove the field (same as <see cref="RemoveField"/>).
    /// </summary>
    public static string SetField(string content, string key, object? value)
    {
        if (value == null)
        {
            return RemoveField(content, key);
        }

        var (fields, body) = Parse(content);
        fields[key] = value;
        return BuildContent(fields, body);
    }

    /// <summary>Removes a field from frontmatter. No-op if absent.</summary>
    public static string RemoveField(string content, string key)
    {
        var (fields, body) = Parse(content);
        if (!fields.Remove(key))
        {
            return content;
        }

        return BuildContent(fields, body);
    }

    // ------------------------------------------------------------------ helpers

    private static int FindFrontmatterEnd(string content)
    {
        var afterOpen = Delimiter.Length;
        // Skip the newline after the opening ---
        if (afterOpen < content.Length && content[afterOpen] == '\r')
        {
            afterOpen++;
        }

        if (afterOpen < content.Length && content[afterOpen] == '\n')
        {
            afterOpen++;
        }

        var searchFrom = afterOpen;
        while (searchFrom < content.Length)
        {
            var idx = content.IndexOf(Delimiter, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
            {
                return -1;
            }

            // Must be at start of line
            if (idx > 0 && content[idx - 1] != '\n')
            {
                searchFrom = idx + 1;
                continue;
            }

            return idx;
        }

        return -1;
    }

    private static Dictionary<string, object?> ParseYaml(string yaml)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var lines = yaml.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');
            i++;

            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0)
            {
                continue;
            }

            var key = line[..colonIdx].Trim();
            var rawValue = line[(colonIdx + 1)..].Trim();

            if (rawValue.StartsWith('['))
            {
                fields[key] = ParseInlineList(rawValue);
            }
            else if (rawValue == "" || rawValue == "|" || rawValue == ">")
            {
                // Block list or multiline — collect indented lines
                var listItems = new List<string>();
                while (i < lines.Length)
                {
                    var next = lines[i].TrimEnd('\r');
                    if (!next.StartsWith("  ") && !next.StartsWith("\t"))
                    {
                        break;
                    }

                    var item = next.TrimStart();
                    if (item.StartsWith("- "))
                    {
                        listItems.Add(item[2..].Trim());
                    }

                    i++;
                }

                fields[key] = listItems.Count > 0 ? (object?)listItems : null;
            }
            else
            {
                fields[key] = ParseScalar(rawValue);
            }
        }

        return fields;
    }

    private static List<string> ParseInlineList(string raw)
    {
        // Handle [a, b, c] or ["a", "b"]
        var inner = raw.TrimStart('[').TrimEnd(']');
        return inner
            .Split(',')
            .Select(s => s.Trim().Trim('"', '\''))
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static object? ParseScalar(string raw)
    {
        if (raw == "null" || raw == "~")
        {
            return null;
        }

        if (raw == "true")
        {
            return true;
        }

        if (raw == "false")
        {
            return false;
        }

        // Strip surrounding quotes
        if ((raw.StartsWith('"') && raw.EndsWith('"')) ||
            (raw.StartsWith('\'') && raw.EndsWith('\'')))
        {
            return raw[1..^1];
        }

        return raw;
    }

    public static string BuildContent(Dictionary<string, object?> fields, string body)
    {
        if (fields.Count == 0)
        {
            return body;
        }

        var sb = new StringBuilder();
        sb.AppendLine(Delimiter);
        foreach (var (key, value) in fields)
        {
            sb.AppendLine(SerializeField(key, value));
        }

        sb.AppendLine(Delimiter);
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }

        return sb.ToString();
    }

    private static string SerializeField(string key, object? value)
    {
        return value switch
        {
            null => $"{key}: null",
            bool b => $"{key}: {(b ? "true" : "false")}",
            List<string> list => $"{key}: [{string.Join(", ", list)}]",
            _ => $"{key}: {EscapeScalar(value.ToString() ?? "")}"
        };
    }

    private static string EscapeScalar(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        // Quote if contains special YAML characters that would confuse a parser
        if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
            value.StartsWith(' ') || value.EndsWith(' '))
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return value;
    }
}
