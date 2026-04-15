public class SanitizeRoute : CommandRoute
{
    public override string? SubCommand => "sanitize";

    public override HelpEntry Help => new HelpEntry(
        "sanitize",
        "Sanitize files (Unicode + WI-NNN to [slug])");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "sanitize")
            return false;

        var items = WorkItemStore.LoadAll(rootPath);
        SanitizeCommand.Execute(rootPath, items);
        return true;
    }
}
