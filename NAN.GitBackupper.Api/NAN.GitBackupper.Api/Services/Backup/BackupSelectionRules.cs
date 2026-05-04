using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Models;
using RepoMode = NAN.GitBackupper.Api.Models.Enums.RepositoryBackupSelectionMode;

namespace NAN.GitBackupper.Api.Services.Backup;

public static class BackupSelectionRules
{
    public static RepoMode GetEffectiveSelectionMode(BackupProfileModel model)
    {
        if (model.RepositoryBackupSelectionMode.HasValue)
            return model.RepositoryBackupSelectionMode.Value;

        return model.UseAllRepositories == false
            ? RepoMode.Manual
            : RepoMode.UseAll;
    }

    public static bool ShouldBackupRepository(BackupProfileModel target, GitRepositoryDescriptor repo)
    {
        return GetEffectiveSelectionMode(target) switch
        {
            RepoMode.UseAll => true,
            RepoMode.OwnRepositoriesOnly => repo.IsOwnedByAuthenticatedUser,
            RepoMode.Manual => target.BackupRepositoryKeys.Any(k =>
                string.Equals(k, repo.DisplayKey, StringComparison.Ordinal)),
            _ => true,
        };
    }
}
