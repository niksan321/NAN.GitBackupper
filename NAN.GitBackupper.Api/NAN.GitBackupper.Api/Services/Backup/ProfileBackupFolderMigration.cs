using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services.Backup;

/// <summary>Перенос каталога данных профиля с пути по «старому» имени (как в БД до обновления) на путь по Guid.</summary>
public static class ProfileBackupFolderMigration
{
    public static void TryMoveLegacyNameFolderToId(BackupProfileModel profile, string nameInDatabaseBeforeUpdate,
        ILogger logger)
    {
        if (profile == null || string.IsNullOrWhiteSpace(nameInDatabaseBeforeUpdate)) return;
        if (string.IsNullOrWhiteSpace((profile.BackupRootPath ?? "").Trim())) return;

        var backupRoot = profile.BackupRootPath.Trim();
        if (string.IsNullOrEmpty(backupRoot)) return;

        string idPath;
        string legacyPath;
        try
        {
            idPath = Path.GetFullPath(BackupPaths.Combine(backupRoot, BackupPaths.GetProfileDataFolderName(profile.Id))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var legacySegment = BackupPaths.SanitizeProfileFolderSegment(nameInDatabaseBeforeUpdate);
            legacyPath = Path.GetFullPath(BackupPaths.Combine(backupRoot, legacySegment)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile backup folder path resolve failed (profile {Id}).", profile.Id);
            return;
        }

        if (string.Equals(legacyPath, idPath, StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(legacyPath)) return;

        if (HasAnyZipInArchives(idPath))
        {
            if (HasAnyZipInArchives(legacyPath))
            {
                logger.LogWarning(
                    "Пропуск переноса: у профиля {ProfileId} и в каталоге по Guid, и в каталоге по имени есть ZIP-архивы. Проверьте данные вручную.",
                    profile.Id);
            }

            return;
        }

        try
        {
            if (!Directory.Exists(idPath))
            {
                Directory.Move(legacyPath, idPath);
                logger.LogInformation("Каталог бэкапа профиля {ProfileId} перенесён на путь по Guid (из папки по старому имени).", profile.Id);
                return;
            }

            if (HasAnyZipInArchives(legacyPath) && !ProfileHasNonEmptyContentExceptReplaceableId(idPath))
            {
                Directory.Delete(idPath, true);
                Directory.Move(legacyPath, idPath);
                logger.LogInformation("Каталог бэкапа профиля {ProfileId} перенесён на путь по Guid (заменён пустой/резервный каталог).", profile.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Не удалось перенести каталог бэкапа профиля {ProfileId} с папки по имени на папку по Guid.",
                profile.Id);
        }
    }

    private static bool HasAnyZipInArchives(string profileRootDirectory)
    {
        if (string.IsNullOrEmpty(profileRootDirectory) || !Directory.Exists(profileRootDirectory)) return false;
        var archives = Path.Combine(profileRootDirectory, BackupDirectoryArchiveService.ArchivesFolderName);
        if (!Directory.Exists(archives)) return false;
        try
        {
            return Directory.EnumerateFiles(archives, "*.zip", SearchOption.TopDirectoryOnly).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool ProfileHasNonEmptyContentExceptReplaceableId(string profileRootDirectory)
    {
        if (string.IsNullOrEmpty(profileRootDirectory) || !Directory.Exists(profileRootDirectory)) return false;
        if (!Directory.EnumerateFileSystemEntries(profileRootDirectory, "*", SearchOption.TopDirectoryOnly).Any()) return false;

        var archives = Path.Combine(profileRootDirectory, BackupDirectoryArchiveService.ArchivesFolderName);
        foreach (var path in Directory.EnumerateFileSystemEntries(profileRootDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(path, archives, StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(archives)) continue;
                if (HasAnyZipInArchives(profileRootDirectory)) return true;
                if (Directory.EnumerateFileSystemEntries(archives, "*", SearchOption.AllDirectories).Any()) return true;
                continue;
            }

            if (File.Exists(path)) return true;
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Any())
                return true;
        }

        return false;
    }
}
