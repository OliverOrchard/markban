public record BoardAddResult(
    BoardAddStatus Status,
    string Key,
    string BoardName,
    string? BoardPath = null,
    bool Bootstrapped = false,
    bool Linked = false,
    bool RootBoardAdded = false,
    string? ErrorMessage = null,
    string? ExistingKey = null)
{
    public bool IsSuccess => Status is BoardAddStatus.Added or BoardAddStatus.DryRun;
}
