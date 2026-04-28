using Microsoft.Extensions.Options;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Models.Enums;
using Quartz;
using Quartz.Impl.Matchers;

namespace NAN.GitBackupper.Api.Scheduling;

public sealed class GitSchedulerCoordinator(
    ISchedulerFactory schedulerFactory,
    IOptions<GitBackupperOptions> options,
    SchedulerState schedulerState,
    ISettingsStore settingsStore,
    ISchedulerRunningPreferenceStore schedulerRunningPreferenceStore,
    ILogger<GitSchedulerCoordinator> logger)
{
    public const string ProfileGroupName = "profiles";

    public async Task SyncJobsWithSettingsAsync(SettingsModel settings, bool schedulerShouldRun, CancellationToken ct = default)
    {
        var sched = await schedulerFactory.GetScheduler(ct);
        await RegisterJobsAsync(sched, settings, ct);
        if (schedulerShouldRun)
        {
            await sched.ResumeAll(ct);
            schedulerState.IsRunning = true;
        }
        else
        {
            await sched.PauseAll(ct);
            schedulerState.IsRunning = false;
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Scheduler start requested");
        var sched = await schedulerFactory.GetScheduler(ct);
        var settings = await settingsStore.LoadAsync(ct);
        await RegisterJobsAsync(sched, settings, ct);
        await sched.ResumeAll(ct);
        schedulerState.IsRunning = true;
        await schedulerRunningPreferenceStore.SetAsync(true, ct);
        logger.LogInformation("Scheduler started");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Scheduler stop requested");
        var sched = await schedulerFactory.GetScheduler(ct);
        await sched.PauseAll(ct);
        schedulerState.IsRunning = false;
        await schedulerRunningPreferenceStore.SetAsync(false, ct);
        logger.LogInformation("Scheduler stopped");
    }

    public async Task OnSettingsSavedAsync(SettingsModel settings, CancellationToken ct = default)
    {
        var sched = await schedulerFactory.GetScheduler(ct);
        await RegisterJobsAsync(sched, settings, ct);
        if (schedulerState.IsRunning)
            await sched.ResumeAll(ct);
        else
            await sched.PauseAll(ct);
    }

    /// <summary>Reconcile Quartz jobs with profiles: add/update first-fire triggers, remove disabled or missing profiles.</summary>
    public async Task RegisterJobsAsync(IScheduler sched, SettingsModel settings, CancellationToken ct)
    {
        if (settings?.TargetItems == null)
            return;

        var desiredIds = new HashSet<Guid>(settings.TargetItems.Where(t => t.Enabled).Select(t => t.Id));
        var existingKeys = await sched.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(ProfileGroupName), ct);
        foreach (var key in existingKeys)
        {
            if (key.Name != null && Guid.TryParse(key.Name, out var gid) && !desiredIds.Contains(gid))
                await sched.DeleteJob(key, ct);
        }

        foreach (var profile in settings.TargetItems.Where(t => t.Enabled))
        {
            var jobKey = new JobKey(profile.Id.ToString(), ProfileGroupName);
            var interval = profile.BackupInterval?.ToTimeSpan() ?? TimeSpan.FromHours(24);
            if (interval < TimeSpan.FromSeconds(1))
                interval = TimeSpan.FromSeconds(1);

            if (!await sched.CheckExists(jobKey, ct))
            {
                var job = JobBuilder.Create<ProfileBackupJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData(ProfileBackupJob.ProfileIdKey, profile.Id.ToString())
                    .Build();
                var trigger = TriggerBuilder.Create()
                    .WithIdentity(profile.Id.ToString(), ProfileGroupName)
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.Add(interval))
                    .Build();
                await sched.ScheduleJob(job, trigger, ct);
            }
        }
    }

    public async Task ApplyInitialHostStateAsync(ISettingsStore settingsStore, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var persisted = await schedulerRunningPreferenceStore.GetAsync(ct);
        var mode = settings.SchedulerStartupPreference ?? SchedulerStartupPreference.ResumeLastState;
        var shouldRun = mode switch
        {
            SchedulerStartupPreference.AlwaysStart => true,
            SchedulerStartupPreference.NeverStart => false,
            SchedulerStartupPreference.ResumeLastState => persisted ?? options.Value.SchedulerAutoStart,
            _ => persisted ?? options.Value.SchedulerAutoStart,
        };
        await SyncJobsWithSettingsAsync(settings, shouldRun, ct);
    }
}
