public class RedoRoute : CommandRoute
{
    public override string? SubCommand => "redo";

    public override HelpEntry Help => new HelpEntry(
        "redo [--dry-run] [--list]",
        "Re-apply the most recently undone reorder or move operation",
        "  --dry-run          preview which files would change without writing\n" +
        "  --list             show the undo and redo history stacks");

    public override bool TryRoute(string[] args, string rootPath) =>
        HistoryRouteDispatcher.TryDispatch(this, "redo", args, rootPath, RedoCommand.Execute);
}
