using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NAN.Git;
using NAN.Git.Models;
using NAN.GitBackupper.Api.Helpers;
using NAN.GitBackupper.Api.Localization;
using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services.Backup;

public sealed class GitBackupService(
    GitProviderClientFactory clientFactory,
    GitProcessRunner gitRunner,
    IBackupUiMessages messages,
    BackupDirectoryArchiveService backupDirectoryArchiveService,
    BackupDirectoryPruneService backupDirectoryPruneService,
    ILogger<GitBackupService> logger)
{
    public async Task<RequestReplay> RunBackupAsync(BackupProfileModel profile,
        IProgress<string> statusProgress = null,
        IProgress<(int Value, int Maximum)> countProgress = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("Backup started for profile {ProfileId} ({ProfileName})", profile.Id, profile.Name);
        statusProgress?.Report(messages.BackupStepPreparing);

        string backupDir;
        try
        {
            backupDir = BackupPaths.GetEffectiveBackupDirectory(profile);
            Directory.CreateDirectory(profile.BackupRootPath);
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare backup directory for profile {ProfileId} ({ProfileName})",
                profile.Id, profile.Name);
            return RequestReplay.Error(ex);
        }

        statusProgress?.Report(messages.BackupStepListingRepositories);

        var gitTarget = profile.AsGitTarget();
        IReadOnlyList<GitRepositoryDescriptor> repos;
        try
        {
            var client = clientFactory.Create(gitTarget);
            repos = await client.ListRepositoriesAsync(gitTarget, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list repositories for profile {ProfileId} ({ProfileName})",
                profile.Id, profile.Name);
            return RequestReplay.Error(ex);
        }

        if (repos.Count == 0)
        {
            logger.LogInformation("Backup finished for profile {ProfileId} ({ProfileName}): no repositories found",
                profile.Id, profile.Name);
            return new RequestReplay
            {
                IsSended = true,
                IsSuccess = true,
                Message = messages.BackupNoRepositories,
            };
        }

        var reposToProcess = repos.Where(r => BackupSelectionRules.ShouldBackupRepository(profile, r)).ToList();

        if (reposToProcess.Count == 0)
        {
            logger.LogInformation(
                "Backup finished for profile {ProfileId} ({ProfileName}): no repositories selected for backup",
                profile.Id, profile.Name);
            return new RequestReplay
            {
                IsSended = true,
                IsSuccess = true,
                Message = messages.BackupNoRepositoriesSelected,
            };
        }

        var repositoryFolderNames = reposToProcess
            .Select(r => GitBackupPathNames.FolderNameFromDisplayKey(r.DisplayKey))
            .ToList();

        try
        {
            var allowedFolderNames = repositoryFolderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            allowedFolderNames.Add(BackupDirectoryArchiveService.ArchivesFolderName);
            statusProgress?.Report(messages.BackupStepCleaningDirectory);
            var cleanProgress = new Progress<(int Value, int Maximum)>(p =>
            {
                countProgress?.Report(p);
                statusProgress?.Report(messages.BackupCleaningProgress(p.Value, p.Maximum));
            });
            await backupDirectoryPruneService.PruneAsync(backupDir, allowedFolderNames, cleanProgress,
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var processorCount = Math.Max(1, Environment.ProcessorCount);
        var rawParallelism = profile.RepositoryBackupParallelism;
        if (rawParallelism < 1)
            rawParallelism = 2;

        var maxParallelism = processorCount * 2;
        var degree = Math.Clamp(rawParallelism, 1, maxParallelism);

        var backupStopwatch = Stopwatch.StartNew();

        List<string> errors = [];
        var errorsLock = new object();
        var okCount = new int[1];
        var processedCount = new int[1];
        var progressLock = new object();

        var total = reposToProcess.Count;
        countProgress?.Report((0, total));
        statusProgress?.Report(messages.BackupProgress(0, total));

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(reposToProcess, parallelOptions, async (repo, ct) =>
        {
            var gitClient = clientFactory.Create(gitTarget);
            var dirName = GitBackupPathNames.FolderNameFromDisplayKey(repo.DisplayKey);
            var localPath = Path.Combine(backupDir, dirName);
            var authUrl = gitClient.BuildAuthenticatedCloneUrl(gitTarget, repo.HttpsCloneUrl);
            var branch = ResolveBackupBranch(profile, repo);

            try
            {
                if (Directory.Exists(Path.Combine(localPath, ".git")))
                {
                    var skipPull = false;
                    if (!string.IsNullOrWhiteSpace(branch))
                    {
                        var fetchResult =
                            await gitRunner.RunAsync(localPath, ["fetch", "--quiet"], ct);
                        if (fetchResult.ExitCode != 0)
                        {
                            lock (errorsLock)
                                errors.Add($"{repo.DisplayKey}: {fetchResult.StdErr}");

                            skipPull = true;
                        }
                        else
                        {
                            var checkoutResult =
                                await gitRunner.RunAsync(localPath, ["checkout", branch], ct);
                            if (checkoutResult.ExitCode != 0)
                            {
                                lock (errorsLock)
                                    errors.Add($"{repo.DisplayKey}: {checkoutResult.StdErr}");

                                skipPull = true;
                            }
                        }
                    }

                    if (!skipPull)
                    {
                        var pullResult =
                            await gitRunner.RunAsync(localPath, ["pull", "--quiet"], ct);
                        if (pullResult.ExitCode != 0)
                        {
                            lock (errorsLock)
                                errors.Add($"{repo.DisplayKey}: {pullResult.StdErr}");
                        }
                        else
                        {
                            Interlocked.Increment(ref okCount[0]);
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(backupDir);
                    var parent = backupDir;
                    var cloneResult = string.IsNullOrWhiteSpace(branch)
                        ? await gitRunner.RunAsync(parent, ["clone", "--quiet", authUrl, localPath],
                            ct)
                        : await gitRunner.RunAsync(parent,
                            ["clone", "--quiet", "-b", branch, "--single-branch", authUrl, localPath],
                            ct);
                    if (cloneResult.ExitCode != 0)
                    {
                        lock (errorsLock)
                            errors.Add($"{repo.DisplayKey}: {cloneResult.StdErr}");
                    }
                    else
                    {
                        Interlocked.Increment(ref okCount[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errorsLock)
                    errors.Add($"{repo.DisplayKey}: {ex.Message}");
            }

            var done = Interlocked.Increment(ref processedCount[0]);
            lock (progressLock)
            {
                countProgress?.Report((done, total));
                statusProgress?.Report(messages.BackupProgress(done, total));
            }
        });

        statusProgress?.Report(messages.BackupStepCalculatingRepositorySizes);
        var sizesTotal = reposToProcess.Count;
        countProgress?.Report((0, sizesTotal));
        var sizeByKey = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; i < reposToProcess.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var repo = reposToProcess[i];
            var dirName = GitBackupPathNames.FolderNameFromDisplayKey(repo.DisplayKey);
            var localPath = Path.Combine(backupDir, dirName);
            var bytes = DirectorySizeHelper.ComputeDirectorySizeBytes(localPath, ct);
            sizeByKey[repo.DisplayKey] = bytes;
            var done = i + 1;
            countProgress?.Report((done, sizesTotal));
            statusProgress?.Report(messages.BackupCalculatingSizesProgress(done, sizesTotal));
        }

        var success = errors.Count == 0;
        var ok = okCount[0];
        var msg = success
            ? messages.BackupFinishedOk(ok, total)
            : messages.BackupFinishedWithErrors(ok, errors.Count,
                string.Join("; ", errors.Take(3)));

        long? zipArchiveBytes = null;
        ArchiveCreateResult? archiveCreated = null;
        statusProgress?.Report(messages.BackupStepCreatingZip);
        try
        {
            var zipCountProgress = new Progress<(int Value, int Maximum)>(p =>
            {
                countProgress?.Report((p.Value, p.Maximum));
                statusProgress?.Report(messages.BackupZipCompressingProgress(p.Value, p.Maximum));
            });
            var zipBundlingStatus = new Progress<string>(s => statusProgress?.Report(s));

            archiveCreated = await backupDirectoryArchiveService.CreateArchiveAsync(profile, repositoryFolderNames,
                degree, messages.BackupZipBundling, zipCountProgress, zipBundlingStatus, ct);
            zipArchiveBytes = archiveCreated.Value.SizeBytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ZIP archive creation failed for profile {ProfileId} ({ProfileName}). BackupDir={BackupDir}",
                profile.Id,
                profile.Name,
                backupDir);
            success = false;
            zipArchiveBytes = null;
            archiveCreated = null;
            msg += " " + messages.BackupZipFailed(ex.Message);
        }

        if (success && archiveCreated is { } created)
        {
            backupStopwatch.Stop();
            BackupArchiveDurationMeta.TryWrite(created.ArchivesDirectory, created.FileName,
                backupStopwatch.ElapsedMilliseconds);
            var zipPath = Path.Combine(created.ArchivesDirectory, created.FileName);
            logger.LogInformation(
                "Backup finished for profile {ProfileId} ({ProfileName}). ZipPath={ZipPath}. ZipSizeBytes={ZipSizeBytes}. DurationMs={DurationMs}",
                profile.Id,
                profile.Name,
                zipPath,
                created.SizeBytes,
                backupStopwatch.ElapsedMilliseconds);
        }
        else if (!success)
        {
            backupStopwatch.Stop();
            logger.LogWarning(
                "Backup stopped with errors for profile {ProfileId} ({ProfileName}). Message={BackupMessage}. DurationMs={DurationMs}",
                profile.Id,
                profile.Name,
                msg.Trim(),
                backupStopwatch.ElapsedMilliseconds);
        }

        var selectedTotal = sizeByKey.Values.Sum();
        return new RequestReplay
        {
            IsSended = true,
            IsSuccess = success,
            Message = msg.Trim(),
            RepositoryBackupSizesBytes = sizeByKey,
            LastBackupSelectedTotalBytes = success ? selectedTotal : null,
            LastBackupZipArchiveBytes = success ? zipArchiveBytes : null,
        };
    }

    private static string ResolveBackupBranch(BackupProfileModel profile, GitRepositoryDescriptor repo)
    {
        if (profile.RepositoryBranches.TryGetValue(repo.DisplayKey, out var saved) &&
            !string.IsNullOrWhiteSpace(saved))
            return saved;

        return string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : repo.DefaultBranch;
    }
}
