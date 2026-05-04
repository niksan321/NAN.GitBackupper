using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NAN.Git;
using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Localization;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Scheduling;
using NAN.GitBackupper.Api.Services;
using NAN.GitBackupper.Api.Services.Backup;

namespace NAN.GitBackupper.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(
    ISettingsStore settingsStore,
    IDbContextFactory<AppDbContext> dbFactory,
    GitProviderClientFactory clientFactory,
    GitBackupService gitBackupService,
    ProfileBackupLock profileBackupLock,
    BackupRunRegistry backupRunRegistry,
    ProfileBackupStatusStore statusStore,
    IBackupProgressLogDismissedStore progressLogDismissedStore,
    IBackupUiMessages messages,
    IOptions<GitBackupperOptions> optionsAccessor,
    ILogger<ProfilesController> logger) : ControllerBase
{
    [HttpPost("{id:guid}/repositories/refresh")]
    public async Task<ActionResult<IReadOnlyList<GitRepositoryDescriptor>>> RefreshRepositories(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var gitTarget = profile.AsGitTarget();
        IReadOnlyList<GitRepositoryDescriptor> repos;
        try
        {
            var client = clientFactory.Create(gitTarget);
            repos = await client.ListRepositoriesAsync(gitTarget, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RefreshRepositories failed for profile {ProfileId}", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.BackupProfiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null)
            return NotFound();

        var model = BackupProfileMapper.ToModel(entity);
        ProfileRepositorySelectionPruner.PruneToKnownRepositories(model, [.. repos.Select(r => r.DisplayKey)]);

        BackupProfileMapper.UpdateEntity(entity, model);
        entity.CachedRepositoriesJson = JsonSerializer.Serialize(repos, RepositoryCacheSerialization.JsonOptions);
        entity.CachedRepositoriesFetchedUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(repos);
    }

    [HttpGet("{id:guid}/repositories")]
    public async Task<ActionResult<IReadOnlyList<GitRepositoryDescriptor>>> ListRepositories(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var gitTarget = profile.AsGitTarget();
        try
        {
            var client = clientFactory.Create(gitTarget);
            var repos = await client.ListRepositoriesAsync(gitTarget, ct);
            return Ok(repos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ListRepositories failed for profile {ProfileId}", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/repositories/branches")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListBranches(Guid id, [FromBody] GitRepositoryDescriptor repository, CancellationToken ct)
    {
        if (repository == null || string.IsNullOrWhiteSpace(repository.DisplayKey))
            return BadRequest();

        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var gitTarget = profile.AsGitTarget();
        try
        {
            var client = clientFactory.Create(gitTarget);
            var branches = await client.ListBranchesAsync(gitTarget, repository, ct);
            return Ok(branches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ListBranches failed for profile {ProfileId}", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/backup/run")]
    public async Task<ActionResult> RunBackup(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var releaser = await profileBackupLock.AcquireAsync(id, ct);
        var runCt = backupRunRegistry.BeginManualRun(id);
        _ = RunBackupWorkAsync(id, profile, settings, releaser, runCt).ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Background backup fault for profile {ProfileId}", id);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        return Accepted();
    }

    private async Task RunBackupWorkAsync(Guid id, BackupProfileModel profile, SettingsModel settings, IDisposable lockReleaser, CancellationToken runCt)
    {
        using (lockReleaser)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await progressLogDismissedStore.SetAsync(id, false, CancellationToken.None);
                statusStore.SetRunning(id, true, messages.BackupStepPreparing);
                var statusProgress = new Progress<string>(msg => statusStore.SetMessage(id, msg));
                var countProgress = new Progress<(int Value, int Maximum)>(p =>
                    statusStore.SetProgress(id, p.Value, p.Maximum));

                var replay = await gitBackupService.RunBackupAsync(profile, statusProgress, countProgress, runCt);

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
                        "Backup completed with errors for profile {ProfileId} ({ProfileName}). Message: {BackupMessage}",
                        id,
                        profile.Name,
                        replay.Message);
                }

                statusStore.SetRunning(id, false, replay.Message ?? "");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Backup stopped by cancellation for profile {ProfileId} ({ProfileName})",
                    id, profile.Name);
                statusStore.SetRunning(id, false, messages.BackupCancelled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backup failed for profile {ProfileId}", id);
                statusStore.SetRunning(id, false, ex.Message);
            }
            finally
            {
                sw.Stop();
                profile.LastBackupDurationMs = sw.ElapsedMilliseconds;
                try
                {
                    await settingsStore.SaveAsync(settings, CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    logger.LogError(saveEx, "Failed to persist profile after backup for profile {ProfileId}", id);
                }

                backupRunRegistry.EndRun(id);
            }
        }
    }

    [HttpPost("{id:guid}/backup/cancel")]
    public ActionResult Cancel(Guid id)
    {
        logger.LogInformation("Manual backup cancellation requested for profile {ProfileId}", id);
        backupRunRegistry.Cancel(id);
        return NoContent();
    }

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ProfileBackupStatus>> GetStatus(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        if (settings.TargetItems?.FirstOrDefault(t => t.Id == id) == null)
            return NotFound();

        var dismissed = await progressLogDismissedStore.GetAsync(id, ct);
        var s = statusStore.GetOrDefault(id);
        s.BackupProgressLogDismissed = dismissed;
        return Ok(s);
    }

    [HttpPost("{id:guid}/backup/progress-log/dismiss")]
    public async Task<ActionResult> DismissBackupProgressLog(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        if (settings.TargetItems?.FirstOrDefault(t => t.Id == id) == null)
            return NotFound();

        await progressLogDismissedStore.SetAsync(id, true, ct);
        await statusStore.RepublishAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/disk-usage")]
    public async Task<ActionResult<ProfileDiskUsageInfo>> GetDiskUsage(Guid id, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(profile.BackupRootPath ?? ""));
            if (string.IsNullOrWhiteSpace(root))
                return Problem(detail: "Не удалось определить диск для BackupRootPath.", statusCode: 500);
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return Problem(detail: "Диск недоступен.", statusCode: 500);

            var total = drive.TotalSize;
            var free = drive.TotalFreeSpace;
            var used = total - free;
            var usedPercent = (int)Math.Round(used * 100d / total, MidpointRounding.AwayFromZero);
            if (usedPercent < 0) usedPercent = 0;
            if (usedPercent > 100) usedPercent = 100;

            return Ok(new ProfileDiskUsageInfo
            {
                TotalBytes = total,
                FreeBytes = free,
                UsedBytes = used,
                DiskUsagePercent = usedPercent,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetDiskUsage failed for profile {ProfileId}", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpGet("{id:guid}/backup-archives")]
    public async Task<ActionResult<IReadOnlyList<BackupArchiveFileInfo>>> ListBackupArchives(Guid id,
        [FromQuery] string sort, [FromQuery] string order, CancellationToken ct)
    {
        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var archivesDir = ProfileArchivesPath.GetProfileArchivesDirectoryOrNull(profile);
        if (archivesDir == null || !Directory.Exists(archivesDir))
            return Ok(Array.Empty<BackupArchiveFileInfo>());

        try
        {
            var list = new List<BackupArchiveFileInfo>();
            foreach (var path in Directory.EnumerateFiles(archivesDir, "*.zip", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(path);
                    var durationMs = BackupArchiveDurationMeta.TryRead(archivesDir, fi.Name);
                    list.Add(new BackupArchiveFileInfo
                    {
                        FileName = fi.Name,
                        SizeBytes = fi.Length,
                        ModifiedUtc = fi.LastWriteTimeUtc,
                        DurationMs = durationMs,
                    });
                }
                catch (FileNotFoundException ex)
                {
                    logger.LogWarning(ex,
                        "Archive file disappeared during list for profile {ProfileId}: {ArchivePath}",
                        id,
                        path);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex,
                        "Archive file I/O error during list for profile {ProfileId}: {ArchivePath}",
                        id,
                        path);
                }
            }

            if (string.IsNullOrWhiteSpace(sort))
                list.Sort((a, b) => b.ModifiedUtc.CompareTo(a.ModifiedUtc));
            else
                TableQuerySort.SortBackupArchives(list, sort, order);
            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ListBackupArchives failed for profile {ProfileId}", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpGet("{id:guid}/backup-archives/download")]
    public async Task<IActionResult> DownloadBackupArchive(Guid id, [FromQuery] string fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
            return BadRequest();
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        var settings = await settingsStore.LoadAsync(ct);
        var profile = settings.TargetItems?.FirstOrDefault(t => t.Id == id);
        if (profile == null)
            return NotFound();

        ProfileBackupRootHelper.ApplyConfiguredRoot(profile, optionsAccessor.Value);

        var archivesDir = ProfileArchivesPath.GetProfileArchivesDirectoryOrNull(profile);
        if (archivesDir == null || !Directory.Exists(archivesDir))
            return NotFound();

        var root = Path.GetFullPath(archivesDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return NotFound();
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, "application/zip", fileName);
    }
}