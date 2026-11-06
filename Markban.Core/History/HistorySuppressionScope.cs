/// <summary>Disposes an active <see cref="HistoryRecorder.Suppress"/> scope.</summary>
internal sealed class HistorySuppressionScope : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public HistorySuppressionScope(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose();
    }
}
