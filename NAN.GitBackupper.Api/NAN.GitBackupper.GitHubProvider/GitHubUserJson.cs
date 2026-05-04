using System.Text.Json.Serialization;

namespace NAN.GitBackupper.GitHubProvider;

internal sealed record GitHubUserJson([property: JsonPropertyName("login")] string Login);