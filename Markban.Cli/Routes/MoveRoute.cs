public class MoveRoute : CommandRoute
{
    public override string? SubCommand => "move";

    public override HelpEntry Help => new HelpEntry(
        "move <id|slug> <lane>",
        "Move an item between lanes (Ideas/Rejected strip number prefix)");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "move")
            return false;

        MoveCommand.Execute(rootPath, args[1], args[2]);
        return true;
    }
}
