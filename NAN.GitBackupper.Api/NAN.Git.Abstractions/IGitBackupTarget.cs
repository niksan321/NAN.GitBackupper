using NAN.Git.Models;

namespace NAN.Git;

/// <summary>
/// Read-only snapshot of backup target data for git and provider API operations.
/// </summary>
public interface IGitBackupTarget
{
    GitProviderKind ProviderKind { get; }

    string ApiKey { get; }

    string ProviderUserName { get; }

    string GitLabBaseUrl { get; }

    string BackupRootPath { get; }

    TimeSpan HttpTimeout { get; }

    IReadOnlyDictionary<string, string> RepositoryBranches { get; }

    bool ShouldBackupRepository(GitRepositoryDescriptor repo);
}