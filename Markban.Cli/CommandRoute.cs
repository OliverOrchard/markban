public abstract class CommandRoute
{
    public abstract bool TryRoute(string[] args, string rootPath);
}
