namespace NAN.GitBackupper.Api.Persistence;

public interface IBackupProgressLogDismissedStore
{
    Task<bool> GetAsync(Guid profileId, CancellationToken ct = default);

    Task SetAsync(Guid profileId, bool dismissed, CancellationToken ct = default);
}
