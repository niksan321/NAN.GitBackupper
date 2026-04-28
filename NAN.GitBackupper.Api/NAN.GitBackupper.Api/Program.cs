using Mapster;
using Microsoft.EntityFrameworkCore;
using NAN.Git;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Endpoints;
using NAN.GitBackupper.Api.Hosting;
using NAN.GitBackupper.Api.Hubs;
using NAN.GitBackupper.Api.Localization;
using NAN.GitBackupper.Api.Mapping;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Scheduling;
using NAN.GitBackupper.Api.Services.Backup;
using Quartz;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GitBackupperOptions>(
    builder.Configuration.GetSection(GitBackupperOptions.SectionName));

var gitBackupperOpts = builder.Configuration.GetSection(GitBackupperOptions.SectionName)
    .Get<GitBackupperOptions>() ?? new GitBackupperOptions();
var sqliteRelative = (gitBackupperOpts.SqliteDatabasePath ?? "data/gitbackupper.db").Trim();
var sqlitePath = Path.Combine(builder.Environment.ContentRootPath, sqliteRelative);
Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={sqlitePath}"));

builder.Services.AddSingleton<IGitLocalizer, EnglishGitLocalizer>();
builder.Services.AddSingleton<IBackupUiMessages, DefaultBackupUiMessages>();
builder.Services.AddSingleton<GitProviderClientFactory>();
builder.Services.AddSingleton<GitProcessRunner>();
builder.Services.AddSingleton<BackupDirectoryArchiveService>();
builder.Services.AddSingleton<BackupDirectoryPruneService>();
builder.Services.AddSingleton<GitBackupService>();
builder.Services.AddSingleton<ISettingsStore, CompositeSettingsStore>();
builder.Services.AddSingleton<IBackupProgressLogDismissedStore, BackupProgressLogDismissedStore>();
builder.Services.AddSingleton<ISchedulerRunningPreferenceStore, SchedulerRunningPreferenceStore>();
builder.Services.AddSingleton<SchedulerState>();
builder.Services.AddSingleton<BackupRunRegistry>();
builder.Services.AddSingleton<ProfileBackupLock>();
builder.Services.AddSingleton<IBackupProgressBroadcaster, SignalRBackupProgressBroadcaster>();
builder.Services.AddSingleton<ProfileBackupStatusStore>();
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddSingleton<GitSchedulerCoordinator>();
builder.Services.AddHostedService<SchedulerInitializationHostedService>();
builder.Services.AddHostedService<OrphanProfileBackupFolderCleanupHostedService>();

builder.Services.AddTransient<ProfileBackupJob>();

builder.Services.AddQuartz();

builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

var mapsterConfig = TypeAdapterConfig.GlobalSettings;
MappingConfig.Register(mapsterConfig);
builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddMapster();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o =>
    {
        o.AddDefaultPolicy(p =>
            p.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

app.UseRouting();
// CORS до статики и SignalR, иначе negotiate с другого origin падает в браузере.
if (corsOrigins.Length > 0)
    app.UseCors();

// Не пропускать POST /hubs/.../negotiate через статику (иначе возможен 405 / неверная обработка).
app.UseWhen(
    ctx => !IsApiOrHubsRequest(ctx.Request),
    branch =>
    {
        branch.UseDefaultFiles();
        branch.UseStaticFiles();
    });

app.MapHub<BackupProgressHub>("/hubs/backup");
app.MapControllers();
ProfileRepositoriesCachedHandler.MapRoutes(app);
// Не отдавать SPA index.html на неизвестные /api/* (иначе Angular получает HTML при ожидании JSON).
app.MapFallback(async (HttpContext http) =>
{
    var virtualPath = NormalizeVirtualPath(http.Request.PathBase, http.Request.Path);
    if (IsApiOrHubsRequest(http.Request))
    {
        var log = http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("NAN.GitBackupper.Api.SpaFallback");
        log.LogWarning(
            "Запрос не сопоставлен с эндпоинтом (ожидался контроллер/минимальный API): {Method} {VirtualPath} (PathBase={PathBase}, Path={Path})",
            http.Request.Method,
            virtualPath,
            http.Request.PathBase,
            http.Request.Path);
        http.Response.StatusCode = StatusCodes.Status404NotFound;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsJsonAsync(
            new { title = "Not Found", status = 404, detail = "No endpoint matched the request.", path = virtualPath },
            http.RequestAborted);
        return;
    }

    var logSpa = http.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("NAN.GitBackupper.Api.SpaFallback");
    logSpa.LogWarning(
        "SPA fallback: отдача wwwroot/index.html для {Method} {VirtualPath} (PathBase={PathBase}, Path={Path})",
        http.Request.Method,
        virtualPath,
        http.Request.PathBase,
        http.Request.Path);

    var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var filePath = Path.Combine(env.WebRootPath ?? string.Empty, "index.html");
    if (!File.Exists(filePath))
    {
        http.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    http.Response.ContentType = "text/html; charset=utf-8";
    await http.Response.SendFileAsync(filePath);
});

app.Run();

public partial class Program
{
    /// <summary>Сначала <see cref="HttpRequest.Path"/> (как у MVC), затем нормализованный PathBase+Path.</summary>
    private static bool IsApiOrHubsRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) return true;
        if (request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)) return true;

        var v = NormalizeVirtualPath(request.PathBase, request.Path);
        return v.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "/api", StringComparison.OrdinalIgnoreCase)
               || v.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "/hubs", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVirtualPath(PathString pathBase, PathString path)
    {
        var pb = pathBase.HasValue ? pathBase.Value!.TrimEnd('/') : string.Empty;
        var p = path.HasValue ? path.Value! : string.Empty;
        if (p.Length == 0) p = "/";
        else if (p[0] != '/') p = "/" + p;
        if (pb.Length == 0) return p;
        var combined = pb + p;
        while (combined.Contains("//", StringComparison.Ordinal))
            combined = combined.Replace("//", "/", StringComparison.Ordinal);
        return combined.Length > 0 ? combined : "/";
    }
}
