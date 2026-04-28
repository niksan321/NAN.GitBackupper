namespace NAN.Git;

public sealed class GitProviderClientFactory(IGitLocalizer localizer)
{
    private readonly GitHubProviderClient gitHubClient = new();
    private readonly GitLabCompatibleProviderClient gitLabClient = new(localizer);
    private readonly BitbucketProviderClient bitbucketClient = new();

    public IGitProviderClient Create(IGitBackupTarget target) =>
        target.ProviderKind switch
        {
            GitProviderKind.GitHub => gitHubClient,
            GitProviderKind.GitLab => gitLabClient,
            GitProviderKind.Other => gitLabClient,
            GitProviderKind.Bitbucket => bitbucketClient,
            _ => gitLabClient,
        };
}
