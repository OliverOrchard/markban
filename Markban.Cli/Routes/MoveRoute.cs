public class MoveRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--move") && !args.Contains("-m"))
            return false;

        var index = Array.FindIndex(args, a => a == "--move" || a == "-m");
        MoveCommand.Execute(rootPath, args[index + 1], args[index + 2]);
        return true;
    }
}
