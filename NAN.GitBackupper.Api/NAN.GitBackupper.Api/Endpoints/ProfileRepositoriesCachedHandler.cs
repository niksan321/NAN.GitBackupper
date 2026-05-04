using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Services;

namespace NAN.GitBackupper.Api.Endpoints;

/// <summary>
/// Явный minimal API-маршрут для кэша репозиториев (тот же URL, что у бывшего экшена MVC),
/// чтобы запрос не уходил в SPA fallback, если MVC-маршрут не матчится.
/// </summary>
public static class ProfileRepositoriesCachedHandler
{
    public static void MapRoutes(WebApplication app)
    {
        app.MapGet("/api/profiles/{id:guid}/repositories/cached", HandleGetCachedAsync)
            .WithName("GetProfileRepositoriesCached");
    }

    private static async Task<IResult> HandleGetCachedAsync(
        Guid id,
        string sort,
        string order,
        ISettingsStore settingsStore,
        IDbContextFactory<AppDbContext> dbFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("ProfileRepositoriesCached");
        var settings = await settingsStore.LoadAsync(ct);
        if (settings.TargetItems?.FirstOrDefault(t => t.Id == id) is null)
            return Results.Problem(statusCode: 404, title: "Не найден", detail: "Профиль не найден.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.BackupProfiles.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null)
            return Results.Text("[]", "application/json; charset=utf-8", Encoding.UTF8);

        const string emptyArrayJson = "[]";
        try
        {
            var raw = entity.CachedRepositoriesJson;
            if (string.IsNullOrWhiteSpace(raw))
                return Results.Text(emptyArrayJson, "application/json; charset=utf-8", Encoding.UTF8);

            var normalized = StripLeadingUnicodeBom(raw.AsSpan().Trim());
            using var doc = JsonDocument.Parse(normalized);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("CachedRepositoriesJson is not a JSON array for profile {ProfileId}", id);
                return Results.Text(emptyArrayJson, "application/json; charset=utf-8", Encoding.UTF8);
            }

            if (string.IsNullOrWhiteSpace(sort))
                return Results.Text(normalized, "application/json; charset=utf-8", Encoding.UTF8);

            var list = JsonSerializer.Deserialize<List<GitRepositoryDescriptor>>(
                normalized, RepositoryCacheSerialization.JsonOptions) ?? [];
            if (list.Count < 2)
                return Results.Text(normalized, "application/json; charset=utf-8", Encoding.UTF8);

            var profileModel = BackupProfileMapper.ToModel(entity);
            TableQuerySort.SortRepositoryDescriptors(
                list,
                profileModel.RepositoryBackupSizesBytes,
                profileModel.RepositoryBranches,
                sort,
                order);
            var sortedJson = JsonSerializer.Serialize(list, RepositoryCacheSerialization.JsonOptions);
            return Results.Text(sortedJson, "application/json; charset=utf-8", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid or unreadable CachedRepositoriesJson for profile {ProfileId}", id);
            return Results.Text(emptyArrayJson, "application/json; charset=utf-8", Encoding.UTF8);
        }
    }

    private static string StripLeadingUnicodeBom(ReadOnlySpan<char> text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];
        return text.ToString();
    }
}