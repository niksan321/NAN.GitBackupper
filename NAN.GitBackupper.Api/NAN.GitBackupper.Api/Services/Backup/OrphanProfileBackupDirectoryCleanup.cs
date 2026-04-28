using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NAN.GitBackupper.Api.Database;

namespace NAN.GitBackupper.Api.Services.Backup;

public static class OrphanProfileBackupDirectoryCleanup
{
    public static async Task RunAsync(
        string backupRootPath,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(backupRootPath)) return;
        var root = Path.GetFullPath(backupRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await db.BackupProfiles.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
            allowed.Add(BackupPaths.GetProfileDataFolderName(id));

        var removed = 0;
        string[] subdirs;
        try
        {
            subdirs = Directory.GetDirectories(root);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Orphan profile backup folder cleanup: cannot list {Root}", root);
            return;
        }

        foreach (var path in subdirs)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (allowed.Contains(name)) continue;
            var full = Path.GetFullPath(path);
            if (!IsStrictChildDirectory(root, full)) continue;
            try
            {
                await DeleteDirectoryForceAsync(full, ct);
                removed++;
                logger.LogInformation("Удалён каталог бэкапа без профиля: {Path}", full);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось удалить каталог бэкапа {Path}", full);
            }
        }

        if (removed > 0)
            logger.LogInformation("Orphan profile backup cleanup: удалено каталогов: {Count}.", removed);
    }

    private static bool IsStrictChildDirectory(string backupRoot, string childPath)
    {
        var fullRoot = Path.GetFullPath(backupRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var rel = Path.GetRelativePath(fullRoot, childPath);
        return rel is not (null or ".." or ".")
               && !rel.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static async Task DeleteDirectoryForceAsync(string path, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            ClearReadOnlyAndSystemAttributes(path);
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(200, ct);
            }
        }
    }

    private static void ClearReadOnlyAndSystemAttributes(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return;

        SetNormalAttributesSafe(rootPath);
        foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
            SetNormalAttributesSafe(entry);
    }

    private static void SetNormalAttributesSafe(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            if ((attrs & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) != 0)
                File.SetAttributes(path, FileAttributes.Normal);
        }
        catch
        {
            // Some file attributes may be unavailable; we'll still attempt directory removal.
        }
    }

}
