namespace NAN.GitBackupper.Api.Scheduling;

public interface IBackupProgressBroadcaster
{
    Task BroadcastAsync(Guid profileId, ProfileBackupStatus status, CancellationToken cancellationToken = default);
}
