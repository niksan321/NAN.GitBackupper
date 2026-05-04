using System.IO.Compression;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Services.Backup;

public readonly record struct ArchiveCreateResult(long SizeBytes, string FileName, string ArchivesDirectory);

public sealed class BackupDirectoryArchiveService(ILogger<BackupDirectoryArchiveService> logger)
{
    public const string ArchivesFolderName = "Archives";

    /// <summary>Сколько файлов *.zip в подпапке Archives каталога бэкапа профиля (0 при отсутствии каталога или ошибке).</summary>
    public int CountZipArchivesForProfileFolder(string backupRootPath, string profileSubfolderSegment)
    {
        if (string.IsNullOrWhiteSpace(backupRootPath))
            return 0;

        try
        {
            var root = BackupPaths.Combine(backupRootPath, profileSubfolderSegment ?? "");
            var archivesDir = Path.Combine(root, ArchivesFolderName);
            if (!Directory.Exists(archivesDir))
                return 0;

            return Directory.EnumerateFiles(archivesDir, "*.zip", SearchOption.TopDirectoryOnly).Count();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Сжимает каждый репозиторий в отдельный ZIP (параллельно, с уровнем сжатия из профиля),
    /// затем собирает один общий ZIP без сжатия и удаляет промежуточные архивы.
    /// </summary>
    /// <returns>Размер, имя файла в Archives и путь к каталогу Archives.</returns>
    public async Task<ArchiveCreateResult> CreateArchiveAsync(BackupProfileModel profile,
        IReadOnlyCollection<string> repositoryFolderNames,
        int maxDegreeOfParallelism,
        string bundlingStatusMessage,
        IProgress<(int Value, int Maximum)> zipProgress,
        IProgress<string> bundlingStatusProgress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repositoryFolderNames);

        var root = Path.GetFullPath(
            BackupPaths.GetEffectiveBackupDirectory(profile).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        var userSegment = SanitizeFileName(PickUserNameSegment(profile));
        var stamp = DateTime.Now.ToString("yyyy.MM.dd_HH-mm-ss");
        var zipFileName = $"{userSegment}.{stamp}.zip";

        var archivesDir = Path.Combine(root, ArchivesFolderName);
        Directory.CreateDirectory(archivesDir);
        var zipPath = Path.Combine(archivesDir, zipFileName);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        var compressionLevel = MapCompression(profile.ZipCompression);
        var folderNamesToZip = repositoryFolderNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => Directory.Exists(Path.Combine(root, n)))
            .ToList();

        var indexedFolders = folderNamesToZip
            .Select((name, i) => (folderName: name, index: i))
            .ToList();

        var total = indexedFolders.Count;
        var degree = Math.Max(1, maxDegreeOfParallelism);
        var staging = Path.Combine(Path.GetTempPath(),
            $"NAN.GitBackupper.pack_{profile.Id:N}_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(staging);

            zipProgress?.Report((0, Math.Max(1, total)));
            var completed = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = degree,
                CancellationToken = ct,
            };

            await Parallel.ForEachAsync(indexedFolders, parallelOptions, async (item, token) =>
            {
                var folderName = item.folderName;
                var subRoot = Path.Combine(root, folderName);
                var safeInnerName = SanitizeFileName(folderName);
                if (string.IsNullOrEmpty(safeInnerName))
                    safeInnerName = "repo";
                var innerZipPath = Path.Combine(staging, $"{safeInnerName}.{item.index:D4}.zip");
                if (File.Exists(innerZipPath))
                    File.Delete(innerZipPath);

                await using (var fs = new FileStream(innerZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                 bufferSize: 32768, useAsync: true))
                {
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);
                    foreach (var filePath in Directory.EnumerateFiles(subRoot, "*", SearchOption.AllDirectories))
                    {
                        token.ThrowIfCancellationRequested();
                        var entryName = Path.GetRelativePath(subRoot, filePath)
                            .Replace(Path.DirectorySeparatorChar, '/');
                        var entry = archive.CreateEntry(entryName, compressionLevel);
                        await using var entryStream = entry.Open();
                        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite, bufferSize: 32768, FileOptions.Asynchronous);
                        await fileStream.CopyToAsync(entryStream, token).ConfigureAwait(false);
                    }
                }

                var done = Interlocked.Increment(ref completed);
                zipProgress?.Report((done, Math.Max(1, total)));
            });

            bundlingStatusProgress?.Report(bundlingStatusMessage ?? "");
            zipProgress?.Report((Math.Max(1, total), Math.Max(1, total)));

            await using (var outerFs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             bufferSize: 32768, useAsync: true))
            {
                using var outerArchive = new ZipArchive(outerFs, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var innerPath in Directory.EnumerateFiles(staging, "*.zip", SearchOption.TopDirectoryOnly)
                             .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    ct.ThrowIfCancellationRequested();
                    var entryName = Path.GetFileName(innerPath).Replace(Path.DirectorySeparatorChar, '_');
                    var entry = outerArchive.CreateEntry(entryName, CompressionLevel.NoCompression);
                    await using var entryStream = entry.Open();
                    await using var innerFs = new FileStream(innerPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 32768, FileOptions.Asynchronous);
                    await innerFs.CopyToAsync(entryStream, ct).ConfigureAwait(false);
                }
            }

            var size = new FileInfo(zipPath).Length;
            TryDeleteDirectory(staging);
            if (profile.KeepArchivesUntilDiskLimit)
                PruneExcessZipArchivesByFreeSpaceTarget(archivesDir, size, 3, zipFileName, ct);
            else
                PruneExcessZipArchives(archivesDir, EffectiveZipArchivesToKeep(profile), zipFileName, ct);
            return new ArchiveCreateResult(size, zipFileName, archivesDir);
        }
        catch (OperationCanceledException)
        {
            TryDeleteZip(zipPath);
            TryDeleteDirectory(staging);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "CreateArchiveAsync failed for profile {ProfileId} ({ProfileName}). Root={Root}, ZipPath={ZipPath}, ArchivesDir={ArchivesDir}",
                profile.Id,
                profile.Name,
                root,
                zipPath,
                archivesDir);
            TryDeleteZip(zipPath);
            TryDeleteDirectory(staging);
            throw;
        }
    }

    private static int EffectiveZipArchivesToKeep(BackupProfileModel profile)
    {
        var n = profile.ZipArchivesToKeep;
        return n < 1 ? 10 : n;
    }

    private static void PruneExcessZipArchives(string archivesDirectory, int maxToKeep, string protectedFileName,
        CancellationToken ct)
    {
        if (maxToKeep < 1 || string.IsNullOrEmpty(archivesDirectory) || !Directory.Exists(archivesDirectory))
            return;

        var files = Directory.EnumerateFiles(archivesDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        while (files.Count > maxToKeep)
        {
            ct.ThrowIfCancellationRequested();
            var oldest = files[0];
            files.RemoveAt(0);
            var name = oldest.Name;
            if (name.Equals(protectedFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                oldest.Delete();
            }
            catch
            {
            }

            BackupArchiveDurationMeta.TryDeleteForZip(archivesDirectory, name);
        }
    }

    private static void PruneExcessZipArchivesByFreeSpaceTarget(string archivesDirectory, long lastZipSizeBytes,
        int futureBackupsToKeepFree, string protectedFileName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(archivesDirectory) || !Directory.Exists(archivesDirectory))
            return;
        if (lastZipSizeBytes <= 0 || futureBackupsToKeepFree <= 0)
            return;

        var requiredFreeBytes = checked(lastZipSizeBytes * futureBackupsToKeepFree);
        var drive = TryResolveDrive(archivesDirectory);
        if (drive == null)
            return;
        if (!drive.IsReady)
            return;
        if (drive.AvailableFreeSpace >= requiredFreeBytes)
            return;

        var files = Directory.EnumerateFiles(archivesDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
            return;

        while (files.Count > 0 && drive.AvailableFreeSpace < requiredFreeBytes)
        {
            ct.ThrowIfCancellationRequested();
            var oldest = files[0];
            files.RemoveAt(0);
            var name = oldest.Name;
            if (name.Equals(protectedFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                oldest.Delete();
            }
            catch
            {
            }

            BackupArchiveDurationMeta.TryDeleteForZip(archivesDirectory, name);
            drive = TryResolveDrive(archivesDirectory);
            if (drive == null || !drive.IsReady)
                break;
        }
    }

    private static DriveInfo TryResolveDrive(string pathOnTargetDisk)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(pathOnTargetDisk));
            if (string.IsNullOrWhiteSpace(root))
                return null;
            return new DriveInfo(root);
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteZip(string zipPath)
    {
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static string PickUserNameSegment(BackupProfileModel profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ProviderUserName))
            return profile.ProviderUserName;
        if (!string.IsNullOrWhiteSpace(profile.Name))
            return profile.Name;
        return "backup";
    }

    private static string SanitizeFileName(string name)
    {
        name ??= "";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrWhiteSpace(name) ? "backup" : name;
    }

    private static CompressionLevel MapCompression(ZipCompressionPreset preset) => preset switch
    {
        ZipCompressionPreset.NoCompression => CompressionLevel.NoCompression,
        ZipCompressionPreset.Fastest => CompressionLevel.Fastest,
        ZipCompressionPreset.Optimal => CompressionLevel.Optimal,
        ZipCompressionPreset.SmallestSize => CompressionLevel.SmallestSize,
        _ => CompressionLevel.Optimal
    };
}
