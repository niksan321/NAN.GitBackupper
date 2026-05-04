using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitLabProvider;

internal sealed record GitLabUserJson([property: JsonPropertyName("id")] long Id);