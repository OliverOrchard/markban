public static class HelpCommand
{
    public static void Execute()
    {
        Console.WriteLine("markban - markdown board CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  --list [-l] [--json]                       List all work items as JSON");
        Console.WriteLine("  --list --folder [-f] <lane>                Filter to a single lane (Todo, InProgress, Testing, Done, Ideas, Rejected)");
        Console.WriteLine("  --summary [-s]                             Combined with --list: ID, Slug, Status only");
        Console.WriteLine("  --id <id>                                  Show a specific item by ID");
        Console.WriteLine("  --slug <slug>                              Show a specific item by Slug");
        Console.WriteLine("  --search <term> [--full]                   Ranked search. --full scans body content.");
        Console.WriteLine("  --next                                     Show the highest priority Todo item");
        Console.WriteLine("  --next-id                                  Print the next safe work item number (max+1)");
        Console.WriteLine("  --move <id|slug> <folder>                  Move an item between lanes (including Ideas, Rejected)");
        Console.WriteLine("                                             Moving TO Ideas/Rejected: strips number prefix");
        Console.WriteLine("                                             Moving FROM Ideas/Rejected: assigns next-id");
        Console.WriteLine("                                             Moving FROM a numbered lane TO Ideas/Rejected auto-compacts the source folder");
        Console.WriteLine("  --reorder <folder> <order> [--dry-run]     Reorder items within a folder");
        Console.WriteLine("              [--no-sub-items]               e.g. --reorder Todo 42,36,34,35");
        Console.WriteLine("  --create \"Title\" [--lane <lane>]           Create a new work item (default lane: Todo)");
        Console.WriteLine("           [--after <id>] [--priority]       --priority inserts at top; --after inserts after <id>");
        Console.WriteLine("  --create \"Title\" --sub-item --parent <id>  Create a sub-item under a parent work item");
        Console.WriteLine("           [--after <id>] [--lane <lane>]    --after <subitem-id> inserts after that sub-item");
        Console.WriteLine("                                             Without --after, appends as the next letter (e.g. 102d)");
        Console.WriteLine("  --overview                                 Print a compact project progress summary (progress bar, active items, backlog counts)");
        Console.WriteLine("  --sanitize                                 Sanitize files (Unicode + WI-NNN -> [slug])");
        Console.WriteLine("  --check-links [--include-ideas]             Find broken [slug] cross-references (with fuzzy suggestions)");
        Console.WriteLine("  --references <slug|id> [--include-ideas]    List all work items that reference [slug]");
        Console.WriteLine("  --git-history <file>                       Show work item activity from git history of a file");
        Console.WriteLine("  --commit <id|slug> --tag <tag>             Move item to Done, then git add / commit / push");
        Console.WriteLine("           --message \"message\"              tag: feat|fix|refactor|test|docs|style|perf|build|ci|chore|revert");
        Console.WriteLine("           [--dry-run]                      message: max 72 chars, one sentence");
        Console.WriteLine("                                             --dry-run: validate, show git status, print planned commands without executing");
        Console.WriteLine("  --root <path>                              Override work-items directory location");
        Console.WriteLine("  web [--port <port>] [--no-open]            Start the web board UI (default port: 5000)");
        Console.WriteLine("  --help [-h]                                Show this help");
    }
}
