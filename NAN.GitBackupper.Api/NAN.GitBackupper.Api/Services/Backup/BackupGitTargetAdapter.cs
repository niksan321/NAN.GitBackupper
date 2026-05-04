using System.Linq;
using NAN.GitBackupper.Api.Models;
using RepoMode = NAN.GitBackupper.Api.Models.Enums.RepositoryBackupSelectionMode;
using ProvType = NAN.GitBackupper.Api.Models.Enums.GitProviderType;
using NAN.Git.Abstractions;

namespace NAN.GitBackupper.Api.Services.Backup;

public sealed class BackupProfileGitTarget(BackupProfileModel model) : IGitBackupTarget
{
    public GitProviderKind ProviderKind => model.Provider switch
    {
        ProvType.GitHub => GitProviderKind.GitHub,
        ProvType.GitLab => GitProviderKind.GitLab,
        ProvType.Bitbucket => GitProviderKind.Bitbucket,
        ProvType.Other => GitProviderKind.Other,
        _ => GitProviderKind.Other,
    };

    public string ApiKey => model.ApiKey;

    public string ProviderUserName => model.ProviderUserName;

    public string GitLabBaseUrl => model.GitLabBaseUrl;

    public string BackupRootPath => model.BackupRootPath;

    public TimeSpan HttpTimeout => TimeSpan.FromSeconds(model.OperationTimeoutSeconds);

    public IReadOnlyDictionary<string, string> RepositoryBranches => model.RepositoryBranches;

    public bool ShouldBackupRepository(GitRepositoryDescriptor repo) =>
        BackupSelectionRules.ShouldBackupRepository(model, repo);
}

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

public static class BackupGitTargetExtensions
{
    public static IGitBackupTarget AsGitTarget(this BackupProfileModel model) => new BackupProfileGitTarget(model);
}
