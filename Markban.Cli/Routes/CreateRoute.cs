public class CreateRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--create"))
            return false;

        var index = Array.IndexOf(args, "--create");
        if (index + 1 >= args.Length)
        {
            Console.WriteLine("Usage: --create \"Title\" [--lane <folder>] [--after <id>] [--priority]");
            return true;
        }
        var title = args[index + 1];
        var lane = "Todo";
        if (args.Contains("--lane"))
        {
            var li = Array.IndexOf(args, "--lane");
            if (li + 1 < args.Length)
            {
                lane = args[li + 1];
            }
        }

        string? afterId = null;
        if (args.Contains("--after"))
        {
            var ai = Array.IndexOf(args, "--after");
            if (ai + 1 < args.Length)
            {
                afterId = args[ai + 1];
            }
        }

        bool topPriority = args.Contains("--priority");

        if (args.Contains("--sub-item"))
        {
            string? parentId = null;
            if (args.Contains("--parent"))
            {
                var pi = Array.IndexOf(args, "--parent");
                if (pi + 1 < args.Length)
                {
                    parentId = args[pi + 1];
                }
            }
            if (string.IsNullOrEmpty(parentId))
            {
                Console.WriteLine("Error: --sub-item requires --parent <id> (e.g. --parent 102).");
                return true;
            }
            CreateCommand.ExecuteSubItem(rootPath, title, parentId, lane, afterId);
        }
        else
        {
            CreateCommand.Execute(rootPath, title, lane, afterId, topPriority);
        }
        return true;
    }
}
