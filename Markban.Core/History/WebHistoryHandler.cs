using Microsoft.AspNetCore.Http;

namespace Markban;

/// <summary>HTTP handlers for <c>GET /api/history</c>, <c>POST /api/undo</c>, and <c>POST /api/redo</c>.</summary>
internal static class WebHistoryHandler
{
    public static IResult GetHistory(string rootPath)
    {
        lock (HistoryGate.Sync)
        {
            var settings = HistorySettings.Load(rootPath);
            if (!settings.Enabled)
            {
                return Results.Ok(new WebHistoryResponse(false, settings.MaxEntries, false, false, [], []));
            }

            var load = HistoryStore.Load(rootPath);
            if (load.IsCorrupt)
            {
                return Results.BadRequest(new { error = load.CorruptWarning });
            }

            var journal = load.Journal;
            return Results.Ok(new WebHistoryResponse(
                true,
                settings.MaxEntries,
                journal.Undo.Count > 0,
                journal.Redo.Count > 0,
                journal.Undo,
                journal.Redo));
        }
    }

    public static IResult Undo(string rootPath) => ToHttpResult(UndoCommand.Execute(rootPath, dryRun: false));

    public static IResult Redo(string rootPath) => ToHttpResult(RedoCommand.Execute(rootPath, dryRun: false));

    private static IResult ToHttpResult(HistoryCommandResult result)
    {
        return result.IsSuccess
            ? Results.Ok(new { message = result.Message, entry = result.Entry })
            : Results.BadRequest(new { error = result.Message });
    }
}
