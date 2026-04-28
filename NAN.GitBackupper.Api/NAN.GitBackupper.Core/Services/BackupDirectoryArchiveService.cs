using System.IO.Compression;
using System.Linq;
using NAN.GitBackupper.Core.Enums;
using NAN.GitBackupper.Core.Models;

namespace NAN.GitBackupper.Core.Services;

public sealed class BackupDirectoryArchiveService
{
    public const string ArchivesFolderName = "Archives";

    /// <summary>Сколько файлов *.zip в подпапке Archives каталога бэкапа профиля (0 при отсутствии каталога или ошибке).</summary>
    public int CountZipArchivesForProfileFolder(string backupRootPath, string profileSubfolderSegment)
    {
        if (string.IsNullOrWhiteSpace(backupRootPath))
            return 0;

        try
        {
            var root = TargetItemBackupPaths.Combine(backupRootPath, profileSubfolderSegment ?? "");
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

    /// <param name="repositoryFolderNames">Подкаталоги внутри корня бекапа (имена папок репозиториев); в архив попадают только они.</param>
    public async Task CreateArchiveAsync(TargetItemModel profile,
        IReadOnlyCollection<string> repositoryFolderNames,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repositoryFolderNames);

        var root = Path.GetFullPath(
            TargetItemBackupPaths.GetEffectiveBackupDirectory(profile).TrimEnd(Path.DirectorySeparatorChar,
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

        try
        {
            await using var fs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 32768, useAsync: true);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var folderName in repositoryFolderNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var subRoot = Path.Combine(root, folderName);
                if (!Directory.Exists(subRoot))
                    continue;

                foreach (var filePath in Directory.EnumerateFiles(subRoot, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    var entryName = Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(entryName, compressionLevel);
                    await using var entryStream = entry.Open();
                    await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, bufferSize: 32768, FileOptions.Asynchronous);
                    await fileStream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
                }
            }

            PruneExcessZipArchives(archivesDir, EffectiveZipArchivesToKeep(profile), ct);
        }
        catch (OperationCanceledException)
        {
            TryDeleteZip(zipPath);
            throw;
        }
        catch
        {
            TryDeleteZip(zipPath);
            throw;
        }
    }

    private static int EffectiveZipArchivesToKeep(TargetItemModel profile)
    {
        var n = profile.ZipArchivesToKeep;
        return n < 1 ? 10 : n;
    }

    private static void PruneExcessZipArchives(string archivesDirectory, int maxToKeep,
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
            try
            {
                oldest.Delete();
            }
            catch
            {
                // ignore per-file cleanup failures
            }
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
            // ignore cleanup failures
        }
    }

    private static string PickUserNameSegment(TargetItemModel profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ProviderUserName))
            return profile.ProviderUserName;
        if (!string.IsNullOrWhiteSpace(profile.Name))
            return profile.Name;
        return "backup";
    }

    private static string SanitizeFileName(string name)
    {
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
