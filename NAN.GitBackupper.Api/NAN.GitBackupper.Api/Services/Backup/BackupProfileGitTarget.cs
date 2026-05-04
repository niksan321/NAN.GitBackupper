using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Services.Backup;

public sealed class BackupProfileGitTarget(BackupProfileModel model) : IGitBackupTarget
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

    public TimeSpan HttpTimeout => TimeSpan.FromSeconds(model.OperationTimeoutSeconds);

    public IReadOnlyDictionary<string, string> RepositoryBranches => model.RepositoryBranches;

    public bool ShouldBackupRepository(GitRepositoryDescriptor repo) =>
        BackupSelectionRules.ShouldBackupRepository(model, repo);
}