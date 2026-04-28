using Microsoft.AspNetCore.SignalR;
using NAN.GitBackupper.Api.Hubs;

namespace NAN.GitBackupper.Api.Scheduling;

public sealed class SignalRBackupProgressBroadcaster(IHubContext<BackupProgressHub> hub) : IBackupProgressBroadcaster
{
    public Task BroadcastAsync(Guid profileId, ProfileBackupStatus status,
        CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(
            "backupProgress",
            new
            {
                profileId,
                isRunning = status.IsRunning,
                // camelCase как у GET /api/profiles/{id}/status (поле lastMessage)
                lastMessage = status.LastMessage ?? "",
                progressValue = status.ProgressValue,
                progressMaximum = status.ProgressMaximum,
                lastUpdatedUtc = status.LastUpdatedUtc,
                backupProgressLogDismissed = status.BackupProgressLogDismissed,
            },
            cancellationToken);
}
