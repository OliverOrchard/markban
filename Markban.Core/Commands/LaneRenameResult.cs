public record LaneRenameResult(
    bool Success,
    string Message,
    bool DryRun = false,
    bool RolledBack = false);
