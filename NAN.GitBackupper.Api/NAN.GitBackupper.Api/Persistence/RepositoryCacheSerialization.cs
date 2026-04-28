using System.Text.Json;
using System.Text.Json.Serialization;

namespace NAN.GitBackupper.Api.Persistence;

/// <summary>Сериализация кэша списка репозиториев в SQLite (тот же контракт, что у HTTP API).</summary>
public static class RepositoryCacheSerialization
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
