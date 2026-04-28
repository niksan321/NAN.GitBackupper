using System.Collections.Concurrent;

namespace NAN.GitBackupper.Api.Scheduling;

public sealed class ProfileBackupLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async Task<IDisposable> AcquireAsync(Guid profileId, CancellationToken ct = default)
    {
        var sem = locks.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
