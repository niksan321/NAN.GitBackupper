using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Services;

namespace NAN.GitBackupper.Api.Persistence;

/// <summary>
/// Настройки приложения (без профилей) в JSON-файле, профили бэкапа — в SQLite через EF Core.
/// </summary>
public sealed class CompositeSettingsStore(
    IDbContextFactory<AppDbContext> dbFactory,
    IWebHostEnvironment environment,
    IOptions<GitBackupperOptions> optionsAccessor,
    ILogger<CompositeSettingsStore> logger) : ISettingsStore
{
    private readonly SemaphoreSlim storeLock = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<SettingsModel> LoadAsync(CancellationToken ct = default)
    {
        await storeLock.WaitAsync(ct);
        try
        {
            var fromFile = await ReadAppSettingsFromFileAsync(ct);
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var dbProfiles = await db.BackupProfiles.AsNoTracking().ToListAsync(ct);
            if (dbProfiles.Count > 0)
            {
                fromFile.TargetItems = dbProfiles.Select(BackupProfileMapper.ToModel).ToList();
                return fromFile;
            }

            if (fromFile.TargetItems is { Count: > 0 })
            {
                var migrated = fromFile.TargetItems;
                foreach (var m in migrated)
                {
                    var entity = BackupProfileMapper.ToEntity(m);
                    db.BackupProfiles.Add(entity);
                }

                await db.SaveChangesAsync(ct);
                fromFile.TargetItems = migrated;
                await SaveAppSettingsToFileWithoutProfilesAsync(fromFile, ct);
                logger.LogInformation("Профили перенесены из {File} в SQLite.", GetSettingsPath());
                return fromFile;
            }

            fromFile.TargetItems = [];
            return fromFile;
        }
        finally
        {
            storeLock.Release();
        }
    }

    public async Task SaveAsync(SettingsModel settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.TargetItems == null)
            settings.TargetItems = [];

        await storeLock.WaitAsync(ct);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var opt = optionsAccessor.Value;
            ProfileBackupRootHelper.ApplyConfiguredRootToAll(settings, opt);

            var incomingIds = new HashSet<Guid>(settings.TargetItems.Select(t => t.Id));
            var existing = await db.BackupProfiles.ToListAsync(ct);
            foreach (var row in existing)
            {
                if (!incomingIds.Contains(row.Id))
                    db.BackupProfiles.Remove(row);
            }

            foreach (var profile in settings.TargetItems)
            {
                profile.ZipAfterBackup = true;
                var entity = await db.BackupProfiles.FindAsync([profile.Id], ct);
                if (entity == null)
                {
                    db.BackupProfiles.Add(BackupProfileMapper.ToEntity(profile));
                }
                else
                {
                    if (profile.LastBackupSelectedTotalBytes == null)
                        profile.LastBackupSelectedTotalBytes = entity.LastBackupSelectedTotalBytes;
                    if (profile.LastBackupZipArchiveBytes == null)
                        profile.LastBackupZipArchiveBytes = entity.LastBackupZipArchiveBytes;
                    if (profile.LastBackupDurationMs == null)
                        profile.LastBackupDurationMs = entity.LastBackupDurationMs;
                    BackupProfileMapper.UpdateEntity(entity, profile);
                }
            }

            await db.SaveChangesAsync(ct);

            await SaveAppSettingsToFileWithoutProfilesAsync(settings, ct);
        }
        finally
        {
            storeLock.Release();
        }
    }

    private async Task<SettingsModel> ReadAppSettingsFromFileAsync(CancellationToken ct)
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return CreateDefault();

        var json = await File.ReadAllTextAsync(path, ct);
        var model = JsonSerializer.Deserialize<SettingsModel>(json, jsonOptions);
        if (model == null)
            return CreateDefault();
        model.TargetItems ??= [];
        foreach (var p in model.TargetItems)
            p.CoalesceOperationTimeoutFromLegacy();
        return model;
    }

    private async Task SaveAppSettingsToFileWithoutProfilesAsync(SettingsModel settings, CancellationToken ct)
    {
        var path = GetSettingsPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var clone = CloneForFile(settings);
        clone.TargetItems = [];

        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(clone, jsonOptions);
        await File.WriteAllTextAsync(temp, json, ct);
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    private static SettingsModel CloneForFile(SettingsModel s) =>
        new()
        {
            TargetItems = [],
            RunOnStartUp = s.RunOnStartUp,
            AutoStartPing = s.AutoStartPing,
            Notify = s.Notify,
            StartMinimized = s.StartMinimized,
            ShowDeletePrompt = s.ShowDeletePrompt,
            PlayErrorSound = s.PlayErrorSound,
            Language = s.Language,
            TimeToShowPopupSec = s.TimeToShowPopupSec,
            SortingColumn = s.SortingColumn,
            SortingDirection = s.SortingDirection,
            SchedulerStartupPreference = s.SchedulerStartupPreference,
        };

    private string GetSettingsPath()
    {
        var name = optionsAccessor.Value.SettingsFileName;
        if (string.IsNullOrWhiteSpace(name))
            name = "settings.json";
        return Path.Combine(environment.ContentRootPath, name);
    }

    private static SettingsModel CreateDefault() =>
        new()
        {
            TargetItems = [],
            AutoStartPing = true,
            RunOnStartUp = false,
            Notify = true,
            StartMinimized = true,
            ShowDeletePrompt = true,
            PlayErrorSound = true,
            SortingColumn = 2,
            SortingDirection = System.ComponentModel.ListSortDirection.Ascending,
            TimeToShowPopupSec = 5,
            Language = "en",
            SchedulerStartupPreference =
                NAN.GitBackupper.Api.Models.Enums.SchedulerStartupPreference.ResumeLastState,
        };
}
