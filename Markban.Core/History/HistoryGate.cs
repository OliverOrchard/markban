/// <summary>Serialises history-affecting operations inside one process (the web server handles requests concurrently).</summary>
internal static class HistoryGate
{
    public static readonly object Sync = new();
}
