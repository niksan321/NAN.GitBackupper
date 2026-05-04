using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitHubProvider;

internal sealed record GitHubBranchJson([property: JsonPropertyName("name")] string Name);