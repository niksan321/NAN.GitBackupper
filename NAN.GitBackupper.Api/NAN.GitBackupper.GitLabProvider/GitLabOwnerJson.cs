using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitLabProvider;

internal sealed record GitLabOwnerJson([property: JsonPropertyName("id")] long Id);