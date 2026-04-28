using Microsoft.EntityFrameworkCore;
using NAN.GitBackupper.Api.Database;

namespace NAN.GitBackupper.Api.Persistence;

public sealed class SchedulerRunningPreferenceStore(IDbContextFactory<AppDbContext> dbFactory)
    : ISchedulerRunningPreferenceStore
{
    public async Task<bool?> GetAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.AppRuntimeState.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == AppRuntimeStateEntity.SingletonId, ct);
        return row == null ? null : row.IsSchedulerRunning;
    }

    public async Task SetAsync(bool isRunning, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.AppRuntimeState
            .FirstOrDefaultAsync(x => x.Id == AppRuntimeStateEntity.SingletonId, ct);
        if (row == null)
        {
            db.AppRuntimeState.Add(new AppRuntimeStateEntity
            {
                Id = AppRuntimeStateEntity.SingletonId,
                IsSchedulerRunning = isRunning,
            });
        }
        else
            row.IsSchedulerRunning = isRunning;

        await db.SaveChangesAsync(ct);
    }
}
