using System.Collections.Concurrent;

namespace NAN.GitBackupper.Api.Scheduling;

public sealed class BackupRunRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> userTokens = new();

    /// <summary>Ручной запуск с API: отмена только через <see cref="Cancel"/>.</summary>
    public CancellationToken BeginManualRun(Guid profileId)
    {
        var userCts = new CancellationTokenSource();
        userTokens[profileId] = userCts;
        return userCts.Token;
    }

    /// <summary>Планировщик Quartz: внешний токен (остановка хоста/джоба) + явная отмена.</summary>
    public CancellationToken BeginRun(Guid profileId, CancellationToken outerToken)
    {
        var userCts = new CancellationTokenSource();
        userTokens[profileId] = userCts;
        return CancellationTokenSource.CreateLinkedTokenSource(outerToken, userCts.Token).Token;
    }

    public void EndRun(Guid profileId)
    {
        if (userTokens.TryRemove(profileId, out var cts))
            cts.Dispose();
    }

    public void Cancel(Guid profileId)
    {
        if (userTokens.TryGetValue(profileId, out var cts))
            cts.Cancel();
    }
}
