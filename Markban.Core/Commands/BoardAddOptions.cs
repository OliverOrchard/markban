public record BoardAddOptions(
    string Name,
    string BoardPath,
    bool NoLink = false,
    bool CopyConfig = false,
    bool DryRun = false);
