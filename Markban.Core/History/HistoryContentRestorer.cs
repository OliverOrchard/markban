using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Hashes work-item files and restores their content (including H1 headings) during undo/redo.</summary>
public static class HistoryContentRestorer
{
    private static readonly Regex NumberPrefixPattern = new(@"^(\d+[a-z]?)-.+\.md$");
    private static readonly Regex HeadingPattern = new(@"^# \d+[a-z]? - (.+)$");
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string ComputeHash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string ComputeFileHash(string fullPath) => ComputeHash(File.ReadAllBytes(fullPath));

    public static string? ExtractNumber(string fileName)
    {
        var match = NumberPrefixPattern.Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static bool CanRestoreWithoutContent(byte[] sourceBytes, HistoryFileState target)
    {
        var restored = RestoreBytes(sourceBytes, target with { Content = null });
        return ComputeHash(restored) == target.Hash;
    }

    public static byte[] RestoreBytes(byte[] currentBytes, HistoryFileState target)
    {
        if (target.Content is not null)
        {
            return Utf8NoBom.GetBytes(target.Content);
        }

        if (ComputeHash(currentBytes) == target.Hash)
        {
            return currentBytes;
        }

        string text = Encoding.UTF8.GetString(currentBytes);
        string updated = target.Number is not null ? ReplaceHeadingNumber(text, target.Number) : text;
        return MatchHashVariant(updated, target.Hash);
    }

    private static string ReplaceHeadingNumber(string text, string newNumber)
    {
        string separator = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int index = ReorderCommand.FindHeadingLine(lines);
        if (index < 0)
        {
            return text;
        }

        var match = HeadingPattern.Match(lines[index]);
        if (!match.Success)
        {
            return text;
        }

        lines[index] = $"# {newNumber} - {match.Groups[1].Value}";
        return string.Join(separator, lines);
    }

    private static byte[] MatchHashVariant(string text, string expectedHash)
    {
        foreach (string candidate in EnumerateLineEndingVariants(text))
        {
            var bytes = Utf8NoBom.GetBytes(candidate);
            if (ComputeHash(bytes) == expectedHash)
            {
                return bytes;
            }
        }

        return Utf8NoBom.GetBytes(text);
    }

    private static IEnumerable<string> EnumerateLineEndingVariants(string text)
    {
        string lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        string trimmedLf = lf.TrimEnd('\n');
        string crlf = trimmedLf.Replace("\n", "\r\n", StringComparison.Ordinal);
        return [text, lf, crlf, trimmedLf + "\n", crlf + "\r\n", trimmedLf, crlf];
    }
}
