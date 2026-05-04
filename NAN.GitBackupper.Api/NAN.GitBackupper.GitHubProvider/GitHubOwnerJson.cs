using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitHubProvider;

internal sealed record GitHubOwnerJson([property: JsonPropertyName("login")] string Login);