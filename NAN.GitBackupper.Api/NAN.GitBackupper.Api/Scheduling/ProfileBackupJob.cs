using System.Diagnostics;
using Microsoft.Extensions.Options;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Services;
using NAN.GitBackupper.Api.Localization;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Services.Backup;
using Quartz;

namespace NAN.GitBackupper.Api.Scheduling;

[DisallowConcurrentExecution]
public sealed class ProfileBackupJob(
    ISchedulerFactory schedulerFactory,
    ISettingsStore settingsStore,
    GitBackupService gitBackupService,
    ProfileBackupLock profileBackupLock,
    BackupRunRegistry backupRunRegistry,
    ProfileBackupStatusStore statusStore,
    IBackupProgressLogDismissedStore progressLogDismissedStore,
    SchedulerState schedulerState,
    IBackupUiMessages messages,
    IOptions<GitBackupperOptions> optionsAccessor,
    ILogger<ProfileBackupJob> logger) : IJob
{
    public const string ProfileIdKey = "ProfileId";

    public async Task Execute(IJobExecutionContext context)
    {
        var idStr = context.MergedJobDataMap.GetString(ProfileIdKey);
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var profileId))
        {
            logger.LogWarning("Profile backup job missing valid {Key}", ProfileIdKey);
            return;
        }

        using (await profileBackupLock.AcquireAsync(profileId, context.CancellationToken))
        {
            var ct = backupRunRegistry.BeginRun(profileId, context.CancellationToken);
            try
            {
                var settings = await settingsStore.LoadAsync(context.CancellationToken);
                var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == profileId);
                if (profile == null)
                {
                    logger.LogWarning("Profile {ProfileId} not found in settings", profileId);
                    return;
                }

                if (!profile.Enabled)
                    return;

                if (!ProfileBackupRootHelper.IsRootConfigured(optionsAccessor.Value))
                {
                    logger.LogWarning("Scheduled backup skipped: GitBackupper:BackupRootPath is not configured");
                    statusStore.SetRunning(profileId, false, "Укажите GitBackupper:BackupRootPath в appsettings.json.");
                    return;
                }

                ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

                await progressLogDismissedStore.SetAsync(profileId, false, context.CancellationToken);
                statusStore.SetRunning(profileId, true, messages.BackupStepPreparing);
                var statusProgress = new Progress<string>(msg => statusStore.SetMessage(profileId, msg));
                var countProgress = new Progress<(int Value, int Maximum)>(p =>
                    statusStore.SetProgress(profileId, p.Value, p.Maximum));

                var sw = Stopwatch.StartNew();
                try
                {
                    var replay = await gitBackupService.RunBackupAsync(profile, statusProgress, countProgress, ct);

                    if (replay.IsSuccess)
                    {
                        if (replay.RepositoryBackupSizesBytes is { Count: > 0 } sizes)
                        {
                            foreach (var kv in sizes)
                                profile.RepositoryBackupSizesBytes[kv.Key] = kv.Value;
                        }

                        if (replay.LastBackupSelectedTotalBytes.HasValue)
                            profile.LastBackupSelectedTotalBytes = replay.LastBackupSelectedTotalBytes;
                        if (replay.LastBackupZipArchiveBytes.HasValue)
                            profile.LastBackupZipArchiveBytes = replay.LastBackupZipArchiveBytes;
                    }
                    else
                    {
                        logger.LogError(
                            "Scheduled backup completed with errors for profile {ProfileId} ({ProfileName}). Message: {BackupMessage}",
                            profileId,
                            profile.Name,
                            replay.Message);
                    }

                    statusStore.SetRunning(profileId, false, replay.Message ?? "");
                }
                finally
                {
                    sw.Stop();
                    profile.LastBackupDurationMs = sw.ElapsedMilliseconds;
                    try
                    {
                        await settingsStore.SaveAsync(settings, context.CancellationToken);
                    }
                    catch (Exception saveEx)
                    {
                        logger.LogError(saveEx,
                            "Failed to persist profile after scheduled backup for profile {ProfileId}", profileId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Scheduled backup stopped by cancellation for profile {ProfileId}", profileId);
                statusStore.SetRunning(profileId, false, messages.BackupCancelled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled backup failed for profile {ProfileId}", profileId);
                statusStore.SetRunning(profileId, false, ex.Message);
            }
            finally
            {
                backupRunRegistry.EndRun(profileId);
            }
        }

        try
        {
            await RescheduleNextAsync(context, profileId, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reschedule backup for profile {ProfileId}", profileId);
        }
    }

    private async Task RescheduleNextAsync(IJobExecutionContext context, Guid profileId, CancellationToken ct)
    {
        if (!schedulerState.IsRunning)
            return;

        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == profileId);
        if (profile == null || !profile.Enabled)
            return;

        var interval = profile.BackupInterval?.ToTimeSpan() ?? TimeSpan.FromHours(24);
        if (interval < TimeSpan.FromSeconds(1))
            interval = TimeSpan.FromSeconds(1);

        var sched = await schedulerFactory.GetScheduler(ct);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(profileId.ToString(), GitSchedulerCoordinator.ProfileGroupName)
            .ForJob(context.JobDetail.Key)
            .StartAt(DateTimeOffset.UtcNow.Add(interval))
            .Build();
        await sched.ScheduleJob(trigger, ct);
    }
}
