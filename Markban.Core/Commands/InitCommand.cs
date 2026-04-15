public static class InitCommand
{
    private static readonly string[] StandardLanes =
        ["Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected"];

    public static void Execute(string workingDir, string? boardPath, string? name, bool dryRun)
    {
        var rootDir = boardPath != null
            ? Path.GetFullPath(Path.Combine(workingDir, boardPath))
            : Path.Combine(workingDir, "work-items");

        var configPath = Path.Combine(workingDir, "markban.json");
        bool needsConfig = boardPath != null || name != null;
        bool configExists = File.Exists(configPath);

        var dirsToCreate = StandardLanes
            .Select(lane => Path.Combine(rootDir, lane))
            .Where(dir => !Directory.Exists(dir))
            .ToList();

        if (dryRun)
        {
            PrintDryRun(rootDir, configPath, needsConfig, configExists, dirsToCreate);
            return;
        }

        foreach (var dir in dirsToCreate)
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"Created {dir}");
        }

        if (dirsToCreate.Count == 0)
            Console.WriteLine($"Board directories already exist at {rootDir}");

        if (needsConfig)
            WriteOrWarnConfig(configPath, configExists, boardPath, name);
    }

    private static void PrintDryRun(
        string rootDir,
        string configPath,
        bool needsConfig,
        bool configExists,
        List<string> dirsToCreate)
    {
        Console.WriteLine("=== DRY RUN --- nothing will be changed ===");
        Console.WriteLine();

        if (dirsToCreate.Count > 0)
        {
            Console.WriteLine("Would create directories:");
            foreach (var dir in dirsToCreate)
                Console.WriteLine($"  {dir}");
        }
        else
        {
            Console.WriteLine($"Board directories already exist at {rootDir}");
        }

        if (needsConfig)
        {
            Console.WriteLine();
            if (configExists)
                Console.WriteLine($"Warning: {configPath} already exists --- would not overwrite.");
            else
                Console.WriteLine($"Would write {configPath}");
        }

        Console.WriteLine();
        Console.WriteLine("=== End dry run. No changes made. ===");
    }

    private static void WriteOrWarnConfig(
        string configPath,
        bool configExists,
        string? boardPath,
        string? name)
    {
        if (configExists)
        {
            Console.WriteLine($"Warning: {configPath} already exists --- not overwritten.");
            return;
        }

        var relPath = boardPath != null
            ? "./" + boardPath.Replace('\\', '/')
            : "./work-items";

        File.WriteAllText(configPath, BuildConfig(relPath, name));
        Console.WriteLine($"Wrote {configPath}");
    }

    private static string BuildConfig(string relPath, string? name)
    {
        var nameSegment = name != null ? string.Concat("\n  \"name\": \"", name, "\",") : "";
        var json = string.Concat(
            "{\n",
            "  \"rootPath\": \"", relPath, "\",", nameSegment, "\n",
            "  \"lanes\": [\n",
            "    { \"name\": \"Todo\",        \"ordered\": true,  \"type\": \"ready\" },\n",
            "    { \"name\": \"In Progress\", \"ordered\": true },\n",
            "    { \"name\": \"Testing\",     \"ordered\": true },\n",
            "    { \"name\": \"Done\",        \"ordered\": true,  \"type\": \"done\" },\n",
            "    { \"name\": \"Ideas\",       \"ordered\": false, \"pickable\": false },\n",
            "    { \"name\": \"Rejected\",    \"ordered\": false, \"pickable\": false }\n",
            "  ],\n",
            "  \"git\": {\n",
            "    \"enabled\": true\n",
            "  },\n",
            "  \"web\": {\n",
            "    \"port\": 5000\n",
            "  }\n",
            "}"
        );
        return json;
    }
}
