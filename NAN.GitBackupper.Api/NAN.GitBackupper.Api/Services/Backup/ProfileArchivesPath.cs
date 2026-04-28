using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services.Backup;

public static class ProfileArchivesPath
{
    public static string GetProfileArchivesDirectoryOrNull(BackupProfileModel profile)
    {
        try
        {
            if (string.IsNullOrWhiteSpace((profile?.BackupRootPath ?? "").Trim())) return null;

            var idBase = Path.GetFullPath(BackupPaths.GetEffectiveBackupDirectory(profile).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var idArchives = Path.GetFullPath(Path.Combine(idBase, BackupDirectoryArchiveService.ArchivesFolderName));
            if (Directory.Exists(idArchives)) return idArchives;

            var namedBase = Path.GetFullPath(BackupPaths
                .Combine(profile.BackupRootPath, BackupPaths.SanitizeProfileFolderSegment(profile.Name))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var nameArchives = Path.GetFullPath(Path.Combine(namedBase, BackupDirectoryArchiveService.ArchivesFolderName));
            if (Directory.Exists(nameArchives)) return nameArchives;
        }
        catch
        {
            // ignored: ответ как раньше — пустой список
        }

        return null;
    }
}
