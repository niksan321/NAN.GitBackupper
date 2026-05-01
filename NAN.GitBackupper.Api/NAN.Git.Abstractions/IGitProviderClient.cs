using NAN.Git.Models;

namespace NAN.Git;

public interface IGitProviderClient
{
    Task<RequestReplay> ValidateAsync(IGitBackupTarget target, CancellationToken ct = default);

    Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(IGitBackupTarget target,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListBranchesAsync(IGitBackupTarget target, GitRepositoryDescriptor repository,
        CancellationToken ct = default);

    string BuildAuthenticatedCloneUrl(IGitBackupTarget target, string httpsCloneUrl);
}