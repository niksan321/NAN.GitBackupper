using System.Text.Json;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Persistence;

public static class BackupProfileMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    public static BackupProfileModel ToModel(BackupProfileEntity e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new BackupProfileModel
        {
            Id = e.Id,
            Provider = (GitProviderType)e.Provider,
            ApiKey = e.ApiKey ?? "",
            ProviderUserName = e.ProviderUserName ?? "",
            GitLabBaseUrl = e.GitLabBaseUrl ?? "",
            BackupRootPath = e.BackupRootPath ?? "",
            Name = e.Name ?? "",
            BackupInterval = new TimePeriod((TimePeriodType)e.BackupIntervalType, e.BackupIntervalValue),
            OperationTimeoutSeconds = e.OperationTimeoutSeconds,
            Enabled = e.Enabled,
            CleanBackupDirectory = e.CleanBackupDirectory,
            ZipAfterBackup = true,
            ZipCompression = (ZipCompressionPreset)e.ZipCompression,
            ZipArchivesToKeep = e.ZipArchivesToKeep,
            KeepArchivesUntilDiskLimit = e.KeepArchivesUntilDiskLimit,
            DiskUsageLimitPercent = e.DiskUsageLimitPercent,
            RepositoryBackupParallelism = e.RepositoryBackupParallelism,
            RepositoryBranches = DeserializeStringDictionary(e.RepositoryBranchesJson),
            UseAllRepositories = e.UseAllRepositories,
            RepositoryBackupSelectionMode = e.RepositoryBackupSelectionMode.HasValue
                ? (RepositoryBackupSelectionMode)e.RepositoryBackupSelectionMode.Value
                : null,
            BackupRepositoryKeys = DeserializeStringList(e.BackupRepositoryKeysJson),
            RepositoryBackupSizesBytes = DeserializeLongDictionary(e.RepositoryBackupSizesBytesJson),
            LastBackupSelectedTotalBytes = e.LastBackupSelectedTotalBytes,
            LastBackupZipArchiveBytes = e.LastBackupZipArchiveBytes,
            LastBackupDurationMs = e.LastBackupDurationMs,
            ShowOnlySelectedRepositories = e.ShowOnlySelectedRepositories,
        };
    }

    public static BackupProfileEntity ToEntity(BackupProfileModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return new BackupProfileEntity
        {
            Id = m.Id,
            Provider = (int)m.Provider,
            ApiKey = m.ApiKey ?? "",
            ProviderUserName = m.ProviderUserName ?? "",
            GitLabBaseUrl = m.GitLabBaseUrl ?? "",
            BackupRootPath = m.BackupRootPath ?? "",
            Name = m.Name ?? "",
            BackupIntervalValue = m.BackupInterval?.Value ?? 24,
            BackupIntervalType = (int)(m.BackupInterval?.Type ?? TimePeriodType.Hours),
            OperationTimeoutSeconds = m.OperationTimeoutSeconds,
            Enabled = m.Enabled,
            CleanBackupDirectory = m.CleanBackupDirectory,
            ZipAfterBackup = m.ZipAfterBackup,
            ZipCompression = (int)m.ZipCompression,
            ZipArchivesToKeep = m.ZipArchivesToKeep,
            KeepArchivesUntilDiskLimit = m.KeepArchivesUntilDiskLimit,
            DiskUsageLimitPercent = m.DiskUsageLimitPercent,
            RepositoryBackupParallelism = m.RepositoryBackupParallelism,
            RepositoryBranchesJson = SerializeStringDictionary(m.RepositoryBranches),
            UseAllRepositories = m.UseAllRepositories,
            RepositoryBackupSelectionMode = m.RepositoryBackupSelectionMode.HasValue
                ? (int)m.RepositoryBackupSelectionMode.Value
                : null,
            BackupRepositoryKeysJson = SerializeStringList(m.BackupRepositoryKeys),
            RepositoryBackupSizesBytesJson = SerializeLongDictionary(m.RepositoryBackupSizesBytes),
            LastBackupSelectedTotalBytes = m.LastBackupSelectedTotalBytes,
            LastBackupZipArchiveBytes = m.LastBackupZipArchiveBytes,
            LastBackupDurationMs = m.LastBackupDurationMs,
            ShowOnlySelectedRepositories = m.ShowOnlySelectedRepositories,
        };
    }

    public static void UpdateEntity(BackupProfileEntity e, BackupProfileModel m)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(m);
        e.Provider = (int)m.Provider;
        e.ApiKey = m.ApiKey ?? "";
        e.ProviderUserName = m.ProviderUserName ?? "";
        e.GitLabBaseUrl = m.GitLabBaseUrl ?? "";
        e.BackupRootPath = m.BackupRootPath ?? "";
        e.Name = m.Name ?? "";
        e.BackupIntervalValue = m.BackupInterval?.Value ?? 24;
        e.BackupIntervalType = (int)(m.BackupInterval?.Type ?? TimePeriodType.Hours);
        e.OperationTimeoutSeconds = m.OperationTimeoutSeconds;
        e.Enabled = m.Enabled;
        e.CleanBackupDirectory = m.CleanBackupDirectory;
        e.ZipAfterBackup = m.ZipAfterBackup;
        e.ZipCompression = (int)m.ZipCompression;
        e.ZipArchivesToKeep = m.ZipArchivesToKeep;
        e.KeepArchivesUntilDiskLimit = m.KeepArchivesUntilDiskLimit;
        e.DiskUsageLimitPercent = m.DiskUsageLimitPercent;
        e.RepositoryBackupParallelism = m.RepositoryBackupParallelism;
        e.RepositoryBranchesJson = SerializeStringDictionary(m.RepositoryBranches);
        e.UseAllRepositories = m.UseAllRepositories;
        e.RepositoryBackupSelectionMode = m.RepositoryBackupSelectionMode.HasValue
            ? (int)m.RepositoryBackupSelectionMode.Value
            : null;
        e.BackupRepositoryKeysJson = SerializeStringList(m.BackupRepositoryKeys);
        e.RepositoryBackupSizesBytesJson = SerializeLongDictionary(m.RepositoryBackupSizesBytes);
        e.LastBackupSelectedTotalBytes = m.LastBackupSelectedTotalBytes;
        e.LastBackupZipArchiveBytes = m.LastBackupZipArchiveBytes;
        e.LastBackupDurationMs = m.LastBackupDurationMs;
        e.ShowOnlySelectedRepositories = m.ShowOnlySelectedRepositories;
    }

    private static Dictionary<string, string> DeserializeStringDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.Ordinal);
        var d = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        if (d == null)
            return new Dictionary<string, string>(StringComparer.Ordinal);
        return new Dictionary<string, string>(d, StringComparer.Ordinal);
    }

    private static Dictionary<string, long> DeserializeLongDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, long>(StringComparer.Ordinal);
        var d = JsonSerializer.Deserialize<Dictionary<string, long>>(json, JsonOptions);
        if (d == null)
            return new Dictionary<string, long>(StringComparer.Ordinal);
        return new Dictionary<string, long>(d, StringComparer.Ordinal);
    }

    private static List<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        return list ?? [];
    }

    private static string SerializeStringDictionary(Dictionary<string, string> d)
    {
        d ??= new Dictionary<string, string>(StringComparer.Ordinal);
        return JsonSerializer.Serialize(d, JsonOptions);
    }

    private static string SerializeLongDictionary(Dictionary<string, long> d)
    {
        d ??= new Dictionary<string, long>(StringComparer.Ordinal);
        return JsonSerializer.Serialize(d, JsonOptions);
    }

    private static string SerializeStringList(List<string> list)
    {
        list ??= [];
        return JsonSerializer.Serialize(list, JsonOptions);
    }
}
