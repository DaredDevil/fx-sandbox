namespace FxSandbox.Services.Locking;

/// <summary>
/// Single-process implementation of <see cref="ILockProvider"/> using
/// <see cref="ReaderWriterLockSlim"/>. Multiple concurrent readers allowed;
/// writes are exclusive. Default registration for single-pod deployments.
/// </summary>
public sealed class LocalLockProvider : ILockProvider
{
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

    public void EnterReadLock()  => _rwLock.EnterReadLock();
    public void ExitReadLock()   => _rwLock.ExitReadLock();
    public void EnterWriteLock() => _rwLock.EnterWriteLock();
    public void ExitWriteLock()  => _rwLock.ExitWriteLock();
    public void Dispose()        => _rwLock.Dispose();
}
