using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services.Backup;

public static class BackupPaths
{
    public static string GetEffectiveBackupDirectory(BackupProfileModel profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Combine(profile.BackupRootPath, GetProfileDataFolderName(profile.Id));
    }

    /// <summary>Подкаталог данных профиля (Guid из БД, формат D), устойчив к смене отображаемого имени.</summary>
    public static string GetProfileDataFolderName(Guid id) =>
        id == default ? "backup" : id.ToString("D");

    /// <summary>Родительский каталог из настроек + сегмент подкаталога (имя папки).</summary>
    public static string Combine(string backupRootPath, string profileSubfolderSegment)
    {
        if (string.IsNullOrWhiteSpace(backupRootPath))
            throw new ArgumentException("Backup root path is empty.", nameof(backupRootPath));

        var root = Path.GetFullPath(backupRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var segment = SanitizeProfileFolderSegment(profileSubfolderSegment);
        return Path.GetFullPath(Path.Combine(root, segment));
    }

    public static string SanitizeProfileFolderSegment(string name)
    {
        var s = name ?? "";
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');

        s = s.Trim();
        return string.IsNullOrWhiteSpace(s) ? "backup" : s;
    }
}
