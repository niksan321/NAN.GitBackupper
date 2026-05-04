using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitLabProvider;

internal sealed record GitLabProjectJson(
    [property: JsonPropertyName("path_with_namespace")] string PathWithNamespace,
    [property: JsonPropertyName("http_url_to_repo")] string HttpUrlToRepo,
    [property: JsonPropertyName("default_branch")] string DefaultBranch,
    [property: JsonPropertyName("owner")] GitLabOwnerJson Owner);