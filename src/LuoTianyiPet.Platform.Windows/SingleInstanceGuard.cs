namespace LuoTianyiPet.Platform.Windows;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, bool isPrimaryInstance)
    {
        _mutex = mutex;
        IsPrimaryInstance = isPrimaryInstance;
    }

    public bool IsPrimaryInstance { get; }

    public static SingleInstanceGuard Acquire(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        Mutex mutex = new(initiallyOwned: true, $"Local\\{applicationId}", out bool createdNew);
        return new SingleInstanceGuard(mutex, createdNew);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already shutting down; releasing is best-effort.
            }
        }

        _mutex.Dispose();
        _disposed = true;
    }
}
