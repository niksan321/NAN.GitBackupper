namespace NAN.Git.Abstractions;

public sealed record GitRepositoryDescriptor(
    string DisplayKey,
    string HttpsCloneUrl,
    string DefaultBranch = null,
    bool IsOwnedByAuthenticatedUser = false);
