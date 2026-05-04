using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitLabProvider;

internal sealed record GitLabBranchJson([property: JsonPropertyName("name")] string Name);