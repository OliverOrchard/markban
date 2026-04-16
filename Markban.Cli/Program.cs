// Parse and strip --root <path> so tests / external callers can override the work-items location
var rootIdx = Array.IndexOf(args, "--root");
string? explicitRoot = null;
var effectiveArgs = args;
if (rootIdx >= 0 && rootIdx + 1 < args.Length)
{
    explicitRoot = Path.GetFullPath(args[rootIdx + 1]);
    effectiveArgs = args.Where((_, i) => i != rootIdx && i != rootIdx + 1).ToArray();
}

// Commands that don't need a work-items directory can run anywhere
bool needsRoot = effectiveArgs.Length > 0
    && !effectiveArgs.Contains("--help")
    && !effectiveArgs.Contains("-h")
    && !(effectiveArgs[0] == "help")
    && !(effectiveArgs[0] == "init");

string rootPath;
if (!needsRoot)
{
    if (explicitRoot != null)
    {
        rootPath = explicitRoot;
    }
    else
    {
        try
        { rootPath = WorkItemStore.FindRoot(); }
        catch (DirectoryNotFoundException) { rootPath = ""; }
    }
}
else
{
    try
    {
        rootPath = explicitRoot ?? WorkItemStore.FindRoot();
    }
    catch (DirectoryNotFoundException)
    {
        Console.Error.WriteLine("Error: No 'work-items' directory found in this directory or any parent.");
        Console.Error.WriteLine("Run 'markban help' for usage, or create a work-items/ folder to get started.");
        return;
    }
}

if (!CommandRouter.Route(effectiveArgs, rootPath))
{
    Console.WriteLine("Unknown command. Use 'markban help' for usage.");
}
