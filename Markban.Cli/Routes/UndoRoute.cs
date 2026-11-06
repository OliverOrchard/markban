public class UndoRoute : CommandRoute
{
    public override string? SubCommand => "undo";

    public override HelpEntry Help => new HelpEntry(
        "undo [--dry-run] [--list]",
        "Undo the most recent reorder or move operation",
        "  --dry-run          preview which files would be restored without writing\n" +
        "  --list             show the undo and redo history stacks");

    public override bool TryRoute(string[] args, string rootPath) =>
        HistoryRouteDispatcher.TryDispatch(this, "undo", args, rootPath, UndoCommand.Execute);
}
