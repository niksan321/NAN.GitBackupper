using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Options;

namespace NAN.GitBackupper.Api.Services;

public static class ProfileBackupRootHelper
{
    public static string GetConfiguredRoot(GitBackupperOptions options)
    {
        return (options?.BackupRootPath ?? "").Trim();
    }

    public static bool IsRootConfigured(GitBackupperOptions options) =>
        !string.IsNullOrWhiteSpace(GetConfiguredRoot(options));

    public static void ApplyConfiguredRoot(BackupProfileModel profile, GitBackupperOptions options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.BackupRootPath = GetConfiguredRoot(options);
    }

    public static void ApplyConfiguredRootToAll(SettingsModel settings, GitBackupperOptions options)
    {
        if (settings?.TargetItems == null)
            return;
        foreach (var p in settings.TargetItems)
            p.BackupRootPath = GetConfiguredRoot(options);
    }

    /// <summary>Проверка уникальности имён профилей (без учёта регистра, после Trim).</summary>
    public static bool HasDuplicateProfileNames(IReadOnlyList<BackupProfileModel> items, out string duplicateName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in items)
        {
            var n = (t.Name ?? "").Trim();
            if (string.IsNullOrEmpty(n))
                continue;
            if (!seen.Add(n))
            {
                duplicateName = n;
                return true;
            }
        }

        duplicateName = "";
        return false;
    }
}
