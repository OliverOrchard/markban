using System.Text;
using System.Text.RegularExpressions;

public static class SanitizeCommand
{
    public static void Execute(string rootPath, List<WorkItem> items)
    {
        int totalFixed = 0;
        var unresolvedByFile = new List<(string FileName, List<string> Refs)>();

        foreach (var item in items)
        {
            var original = File.ReadAllText(item.FullPath);
            var sanitized = SanitizeText(original);
            var (result, unresolved) = FixReferences(sanitized, items);
            sanitized = result;

            if (original != sanitized)
            {
                File.WriteAllText(item.FullPath, sanitized, new UTF8Encoding(false));
                Console.WriteLine($"Sanitized & Fixed Refs: {item.FileName}");
                totalFixed++;
            }

            if (unresolved.Count > 0)
            {
                unresolvedByFile.Add((item.FileName, unresolved));
            }
        }

        Console.WriteLine($"Done. Processed {totalFixed} files.");

        if (unresolvedByFile.Count > 0)
        {
            Console.WriteLine($"\nWarning: {unresolvedByFile.Count} file(s) contain unresolvable numeric references (stale after a renumber -- fix manually):");
            foreach (var (fileName, refs) in unresolvedByFile)
            {
                Console.WriteLine($"  {fileName}: {string.Join(", ", refs.Distinct())}");
            }
        }
    }

    public static string SanitizeText(string text)
    {
        var map = new Dictionary<char, string>
        {
            // Dashes
            { (char)0x2014, "--" }, { (char)0x2013, "-" }, { (char)0x2012, "-" }, { (char)0x2015, "--" }, { (char)0x2212, "-" },
            // Quotes
            { (char)0x201C, "\"" }, { (char)0x201D, "\"" }, { (char)0x2018, "'" }, { (char)0x2019, "'" }, { (char)0x201A, "," }, { (char)0x201E, "\"" },
            { (char)0x00AB, "<<" }, { (char)0x00BB, ">>" }, { (char)0x2039, "<" }, { (char)0x203A, ">" },
            // Dots
            { (char)0x2026, "..." }, { (char)0x00B7, "." },
            // Spaces
            { (char)0x00A0, " " }, { (char)0x202F, " " }, { (char)0x2009, " " }, { (char)0x200A, " " }, { (char)0x205F, " " }, { (char)0x3000, " " },
            { (char)0x000B, " " }, { (char)0x000C, " " },
            // Zero-width (remove)
            { (char)0x200B, "" }, { (char)0x200C, "" }, { (char)0x200D, "" }, { (char)0xFEFF, "" },
            // Symbols
            { (char)0x00D7, "x" }, { (char)0x00F7, "/" }, { (char)0x2022, "-" }, { (char)0x25CF, "-" }, { (char)0x2192, "->" }, { (char)0x2190, "<-" },
            { (char)0x21D2, "=>" }, { (char)0x2248, "~=" }, { (char)0x2260, "!=" }, { (char)0x2264, "<=" }, { (char)0x2265, ">=" },
            { (char)0x2032, "'" }, { (char)0x2033, "''" }, { (char)0x00AE, "(r)" }, { (char)0x00A9, "(c)" }, { (char)0x2122, "(tm)" }, { (char)0x00B0, " deg" }
        };

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (map.TryGetValue(c, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static (string Text, List<string> UnresolvedRefs) FixReferences(string text, List<WorkItem> allItems)
    {
        var idToSlug = allItems
            .Where(i => !string.IsNullOrEmpty(i.Id))
            .DistinctBy(i => i.Id)
            .ToDictionary(i => i.Id, i => i.Slug, StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();

        // Pattern 1: WI-123 (explicit prefix — warn if unresolvable)
        // Pattern 2: [123]   (bare bracket numbers — convert silently, ignore if not found)
        var patterns = new[] {
            @"\bWI-(\d+[a-z]?)\b",
            @"\[(\d+[a-z]?)\]"
        };

        var result = text;
        foreach (var pattern in patterns)
        {
            var isPrefixed = pattern.StartsWith(@"\bWI");
            result = Regex.Replace(result, pattern, m =>
            {
                var id = m.Groups[1].Value;
                if (idToSlug.TryGetValue(id, out var slug))
                {
                    return $"[{slug}]";
                }
                if (isPrefixed)
                {
                    unresolved.Add(m.Value);
                }
                return m.Value;
            }, RegexOptions.IgnoreCase);
        }

        return (result, unresolved);
    }
}
