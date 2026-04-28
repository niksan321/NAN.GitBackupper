using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services;

/// <summary>Удаляет из настроек профиля ключи репозиториев, отсутствующие в актуальном списке с API.</summary>
public static class ProfileRepositorySelectionPruner
{
    public static void PruneToKnownRepositories(BackupProfileModel profile, IReadOnlyCollection<string> validDisplayKeys)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(validDisplayKeys);
        var valid = new HashSet<string>(validDisplayKeys, StringComparer.Ordinal);
        profile.BackupRepositoryKeys = profile.BackupRepositoryKeys.Where(valid.Contains).ToList();
        profile.RepositoryBranches = profile.RepositoryBranches
            .Where(kv => valid.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        profile.RepositoryBackupSizesBytes = profile.RepositoryBackupSizesBytes
            .Where(kv => valid.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }
}
