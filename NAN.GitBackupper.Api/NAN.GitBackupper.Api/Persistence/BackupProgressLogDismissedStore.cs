using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NAN.GitBackupper.Api.Database;

namespace NAN.GitBackupper.Api.Persistence;

public sealed class BackupProgressLogDismissedStore(IDbContextFactory<AppDbContext> dbFactory)
    : IBackupProgressLogDismissedStore
{
    private readonly ConcurrentDictionary<Guid, bool> cache = new();

    public async Task<bool> GetAsync(Guid profileId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(profileId, out var cached))
            return cached;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var dismissed = await db.BackupProfiles.AsNoTracking()
            .Where(p => p.Id == profileId)
            .Select(p => (bool?)p.BackupProgressLogDismissed)
            .FirstOrDefaultAsync(ct);
        var value = dismissed ?? false;
        cache[profileId] = value;
        return value;
    }

    public async Task SetAsync(Guid profileId, bool dismissed, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var affected = await db.BackupProfiles.Where(p => p.Id == profileId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.BackupProgressLogDismissed, dismissed), ct);
        if (affected == 0)
            return;
        cache[profileId] = dismissed;
    }
}
