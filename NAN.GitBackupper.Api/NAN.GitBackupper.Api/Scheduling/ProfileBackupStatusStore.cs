using System.Collections.Concurrent;
using NAN.GitBackupper.Api.Persistence;

namespace NAN.GitBackupper.Api.Scheduling;

public sealed class ProfileBackupStatusStore(
    IBackupProgressBroadcaster broadcaster,
    IBackupProgressLogDismissedStore dismissStore)
{
    private readonly ConcurrentDictionary<Guid, ProfileBackupStatus> statuses = new();

    public void SetRunning(Guid profileId, bool isRunning, string message = null)
    {
        statuses.AddOrUpdate(profileId,
            _ => new ProfileBackupStatus
            {
                IsRunning = isRunning,
                LastMessage = message ?? "",
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = 0,
                ProgressMaximum = 1,
                BackupProgressLogDismissed = false,
            },
            (_, prev) => new ProfileBackupStatus
            {
                IsRunning = isRunning,
                LastMessage = message ?? prev.LastMessage,
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = isRunning ? 0 : prev.ProgressValue,
                ProgressMaximum = isRunning ? 1 : prev.ProgressMaximum,
                BackupProgressLogDismissed = isRunning ? false : prev.BackupProgressLogDismissed,
            });
        _ = PublishAsync(profileId);
    }

    public void SetMessage(Guid profileId, string message)
    {
        statuses.AddOrUpdate(profileId,
            _ => new ProfileBackupStatus
            {
                IsRunning = true,
                LastMessage = message ?? "",
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = 0,
                ProgressMaximum = 1,
                BackupProgressLogDismissed = false,
            },
            (_, prev) => new ProfileBackupStatus
            {
                IsRunning = prev.IsRunning,
                LastMessage = message ?? prev.LastMessage,
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = prev.ProgressValue,
                ProgressMaximum = prev.ProgressMaximum,
                BackupProgressLogDismissed = prev.BackupProgressLogDismissed,
            });
        _ = PublishAsync(profileId);
    }

    public void SetProgress(Guid profileId, int value, int maximum)
    {
        var max = Math.Max(1, maximum);
        var v = Math.Clamp(value, 0, max);
        statuses.AddOrUpdate(profileId,
            _ => new ProfileBackupStatus
            {
                IsRunning = true,
                LastMessage = "",
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = v,
                ProgressMaximum = max,
                BackupProgressLogDismissed = false,
            },
            (_, prev) => new ProfileBackupStatus
            {
                IsRunning = prev.IsRunning,
                LastMessage = prev.LastMessage,
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressValue = v,
                ProgressMaximum = max,
                BackupProgressLogDismissed = prev.BackupProgressLogDismissed,
            });
        _ = PublishAsync(profileId);
    }

    public ProfileBackupStatus GetOrDefault(Guid profileId)
    {
        return statuses.TryGetValue(profileId, out var s)
            ? s
            : new ProfileBackupStatus
            {
                LastMessage = "",
                LastUpdatedUtc = DateTime.UtcNow,
                ProgressMaximum = 1,
                BackupProgressLogDismissed = false,
            };
    }

    public async Task RepublishAsync(Guid profileId, CancellationToken ct = default)
    {
        if (!statuses.TryGetValue(profileId, out var s))
            return;
        var dismissed = await dismissStore.GetAsync(profileId, ct).ConfigureAwait(false);
        var updated = new ProfileBackupStatus
        {
            IsRunning = s.IsRunning,
            LastMessage = s.LastMessage,
            LastUpdatedUtc = s.LastUpdatedUtc,
            ProgressValue = s.ProgressValue,
            ProgressMaximum = s.ProgressMaximum,
            BackupProgressLogDismissed = dismissed,
        };
        statuses[profileId] = updated;
        try
        {
            await broadcaster.BroadcastAsync(profileId, updated, ct).ConfigureAwait(false);
        }
        catch
        {
            // не роняем бекап из‑за отсутствия подписчиков SignalR
        }
    }

    private async Task PublishAsync(Guid profileId)
    {
        if (!statuses.TryGetValue(profileId, out var s))
            return;
        try
        {
            await broadcaster.BroadcastAsync(profileId, s).ConfigureAwait(false);
        }
        catch
        {
            // не роняем бекап из‑за отсутствия подписчиков SignalR
        }
    }
}

public sealed class ProfileBackupStatus
{
    public bool IsRunning { get; set; }
    public string LastMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    /// <summary>Текущее значение счётчика шагов (как в WPF ProgressBar).</summary>
    public int ProgressValue { get; set; }
    /// <summary>Верхняя граница; минимум 1.</summary>
    public int ProgressMaximum { get; set; } = 1;
    /// <summary>Соответствует <c>BackupProfiles.BackupProgressLogDismissed</c> в SQLite.</summary>
    public bool BackupProgressLogDismissed { get; set; }
}
