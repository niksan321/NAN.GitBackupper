namespace NAN.Git.Models;

public sealed record GitRepositoryDescriptor(
    string DisplayKey,
    string HttpsCloneUrl,
    string DefaultBranch = null,
    bool IsOwnedByAuthenticatedUser = false);
