using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitHubProvider;

internal sealed record GitHubRepoJson([property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("clone_url")] string CloneUrl,
    [property: JsonPropertyName("default_branch")] string DefaultBranch,
    [property: JsonPropertyName("owner")] GitHubOwnerJson Owner);