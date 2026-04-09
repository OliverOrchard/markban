using Markban;

public class WebRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "web")
            return false;

        int port = 5000;
        bool noOpen = false;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
            {
                port = p;
                i++;
            }
            else if (args[i] == "--no-open")
            {
                noOpen = true;
            }
        }

        WebServer.Run(rootPath, port, noOpen);
        return true;
    }
}
