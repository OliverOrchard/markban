using System.Text.Json;

public static class InitCommand
{
    public static void Execute(string workingDir, string? boardPath, string? name, bool dryRun)
    {
        var configPath = Path.Combine(workingDir, "markban.json");

        if (File.Exists(configPath))
        {
            ExecuteWithExistingConfig(workingDir, configPath, boardPath, name, dryRun);
        }
        else
        {
            ExecuteWithNewConfig(workingDir, configPath, boardPath, name, dryRun);
        }
    }

    private static void ExecuteWithExistingConfig(
        string workingDir, string configPath, string? boardPath, string? name, bool dryRun)
    {
        bool hasIgnoredFlags = boardPath != null || name != null;

        string rootDir;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            string? relPath = null;
            if (doc.RootElement.TryGetProperty("rootPath", out var pathEl))
            {
                relPath = pathEl.GetString();
            }

            rootDir = !string.IsNullOrWhiteSpace(relPath)
                ? Path.GetFullPath(Path.Combine(workingDir, relPath))
                : Path.Combine(workingDir, "work-items");
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Error: Cannot parse {configPath}.");
            return;
        }

        var lanes = WorkItemStore.LoadConfig(rootDir);
        var dirsToCreate = lanes
            .Select(l => Path.Combine(rootDir, l.Name))
            .Where(d => !Directory.Exists(d))
            .ToList();

        if (dryRun)
        {
            Console.WriteLine("=== DRY RUN --- nothing will be changed ===");
            Console.WriteLine();
            if (hasIgnoredFlags)
            {
                Console.WriteLine($"Warning: {configPath} already exists -- --path/--name flags ignored.");
            }

            Console.WriteLine($"Found existing config at {configPath} --- would not overwrite.");
            Console.WriteLine();
            if (dirsToCreate.Count > 0)
            {
                Console.WriteLine("Would create directories:");
                foreach (var dir in dirsToCreate)
                {
                    Console.WriteLine($"  {dir}");
                }
            }
            else
            {
                Console.WriteLine($"All lane directories already exist at {rootDir}");
            }
            Console.WriteLine();
            Console.WriteLine("=== End dry run. No changes made. ===");
            return;
        }

        if (hasIgnoredFlags)
        {
            Console.WriteLine($"Warning: {configPath} already exists --- not overwritten. --path/--name flags ignored.");
        }

        foreach (var dir in dirsToCreate)
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"Created {dir}");
        }

        if (dirsToCreate.Count == 0)
        {
            Console.WriteLine($"All lane directories already exist at {rootDir}");
        }
        else
        {
            Console.WriteLine($"Board ready at {rootDir}");
        }
    }

    private static void ExecuteWithNewConfig(
        string workingDir, string configPath, string? boardPath, string? name, bool dryRun)
    {
        var relPath = boardPath != null
            ? "./" + boardPath.Replace('\\', '/')
            : "./work-items";
        var rootDir = boardPath != null
            ? Path.GetFullPath(Path.Combine(workingDir, boardPath))
            : Path.Combine(workingDir, "work-items");

        var configContent = BuildConfig(relPath, name);

        var dirsToCreate = BoardConfig.DefaultLanes
            .Select(l => Path.Combine(rootDir, l.Name))
            .Where(d => !Directory.Exists(d))
            .ToList();

        if (dryRun)
        {
            Console.WriteLine("=== DRY RUN --- nothing will be changed ===");
            Console.WriteLine();
            Console.WriteLine($"Would write {configPath}:");
            Console.WriteLine(configContent);
            Console.WriteLine();
            if (dirsToCreate.Count > 0)
            {
                Console.WriteLine("Would create directories:");
                foreach (var dir in dirsToCreate)
                {
                    Console.WriteLine($"  {dir}");
                }
            }
            else
            {
                Console.WriteLine($"All lane directories already exist at {rootDir}");
            }
            Console.WriteLine();
            Console.WriteLine("=== End dry run. No changes made. ===");
            return;
        }

        File.WriteAllText(configPath, configContent);
        Console.WriteLine($"Wrote {configPath}");

        foreach (var dir in dirsToCreate)
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"Created {dir}");
        }

        if (dirsToCreate.Count == 0)
        {
            Console.WriteLine($"All lane directories already exist at {rootDir}");
        }
    }

    private static string BuildConfig(string relPath, string? name)
    {
        var nameSegment = name != null ? $"\n  \"name\": \"{name}\"," : "";
        return string.Concat(
            "{\n",
            $"  \"rootPath\": \"{relPath}\",{nameSegment}\n",
            "  \"lanes\": [\n",
            "    { \"name\": \"Todo\",        \"ordered\": true,  \"type\": \"ready\" },\n",
            "    { \"name\": \"In Progress\", \"ordered\": true },\n",
            "    { \"name\": \"Testing\",     \"ordered\": true },\n",
            "    { \"name\": \"Done\",        \"ordered\": true,  \"type\": \"done\" },\n",
            "    { \"name\": \"Ideas\",       \"ordered\": false, \"pickable\": false },\n",
            "    { \"name\": \"Rejected\",    \"ordered\": false, \"pickable\": false }\n",
            "  ],\n",
            "  \"heading\": {\n",
            "    \"enabled\": true\n",
            "  },\n",
            "  \"slugs\": {\n",
            "    \"casing\": \"kebab\"\n",
            "  },\n",
            "  \"commit\": {\n",
            "    \"maxMessageLength\": 72,\n",
            "    \"tags\": [\"feat\", \"fix\", \"docs\", \"style\", \"refactor\", \"test\", \"build\", \"ci\", \"chore\", \"revert\", \"perf\"]\n",
            "  },\n",
            "  \"git\": {\n",
            "    \"enabled\": true,\n",
            "    \"featureBranches\": {\n",
            "      \"enabled\": false,\n",
            "      \"mainBranch\": \"main\",\n",
            "      \"commitStrategy\": \"single\",\n",
            "      \"pullOnStart\": true,\n",
            "      \"checkoutOnDone\": true,\n",
            "      \"prCommand\": \"\"\n",
            "    }\n",
            "  },\n",
            "  \"web\": {\n",
            "    \"port\": 5000\n",
            "  },\n",
            "  \"blocked\": {\n",
            "    \"enabled\": true\n",
            "  },\n",
            "  \"tags\": {\n",
            "    \"enabled\": true\n",
            "  },\n",
            "  \"dependsOn\": {\n",
            "    \"enabled\": true\n",
            "  },\n",
            "  \"customFrontmatter\": []\n",
            "}"
        );
    }
}
