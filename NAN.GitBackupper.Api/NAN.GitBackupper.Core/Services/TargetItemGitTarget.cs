using System.Linq;
using NAN.Git.Abstractions;
using NAN.GitBackupper.Core.Enums;
using NAN.GitBackupper.Core.Models;

namespace NAN.GitBackupper.Core.Services;

public sealed class TargetItemModelGitTarget(TargetItemModel model) : IGitBackupTarget
{
    public GitProviderKind ProviderKind => model.Provider switch
    {
        GitProviderType.GitHub => GitProviderKind.GitHub,
        GitProviderType.GitLab => GitProviderKind.GitLab,
        GitProviderType.Bitbucket => GitProviderKind.Bitbucket,
        GitProviderType.Other => GitProviderKind.Other,
        _ => GitProviderKind.Other,
    };

    public string ApiKey => model.ApiKey;

    public string ProviderUserName => model.ProviderUserName;

    public string GitLabBaseUrl => model.GitLabBaseUrl;

    public string BackupRootPath => model.BackupRootPath;

    public TimeSpan HttpTimeout => model.OperationTimeout.ToTimeSpan();

    public IReadOnlyDictionary<string, string> RepositoryBranches => model.RepositoryBranches;

    public bool ShouldBackupRepository(GitRepositoryDescriptor repo) =>
        TargetItemBackupRules.ShouldBackupRepository(model, repo);
}

public static class TargetItemBackupRules
{
    public static RepositoryBackupSelectionMode GetEffectiveSelectionMode(TargetItemModel model)
    {
        if (model.RepositoryBackupSelectionMode.HasValue)
            return model.RepositoryBackupSelectionMode.Value;

        return model.UseAllRepositories == false
            ? RepositoryBackupSelectionMode.Manual
            : RepositoryBackupSelectionMode.UseAll;
    }

    public static bool ShouldBackupRepository(TargetItemModel target, GitRepositoryDescriptor repo)
    {
        return GetEffectiveSelectionMode(target) switch
        {
            RepositoryBackupSelectionMode.UseAll => true,
            RepositoryBackupSelectionMode.OwnRepositoriesOnly => repo.IsOwnedByAuthenticatedUser,
            RepositoryBackupSelectionMode.Manual => target.BackupRepositoryKeys.Any(k =>
                string.Equals(k, repo.DisplayKey, StringComparison.Ordinal)),
            _ => true,
        };
    }
}

public static class TargetItemGitTargetExtensions
{
    public static IGitBackupTarget AsGitTarget(this TargetItemModel model) => new TargetItemModelGitTarget(model);
}
