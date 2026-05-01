namespace NAN.GitBackupper.Api.Database;

/// <summary>Сущность профиля бэкапа в SQLite (сложные поля — JSON в текстовых колонках).</summary>
public sealed class BackupProfileEntity
{
    public Guid Id { get; set; }

    public int Provider { get; set; }

    public string ApiKey { get; set; }

    public string ProviderUserName { get; set; }

    public string GitLabBaseUrl { get; set; }

    public string BackupRootPath { get; set; }

    public string Name { get; set; }

    public int BackupIntervalValue { get; set; }

    public int BackupIntervalType { get; set; }

    /// <summary>Таймаут HTTP (секунды), эквивалент <see cref="System.TimeSpan"/> в коде.</summary>
    public int OperationTimeoutSeconds { get; set; }

    public bool Enabled { get; set; }

    public bool CleanBackupDirectory { get; set; }

    public bool ZipAfterBackup { get; set; }

    public int ZipCompression { get; set; }

    public int ZipArchivesToKeep { get; set; }

    public bool KeepArchivesUntilDiskLimit { get; set; }

    public int DiskUsageLimitPercent { get; set; }

    public int RepositoryBackupParallelism { get; set; }

    public string RepositoryBranchesJson { get; set; }

    public bool? UseAllRepositories { get; set; }

    public int? RepositoryBackupSelectionMode { get; set; }

    public string BackupRepositoryKeysJson { get; set; }

    public string RepositoryBackupSizesBytesJson { get; set; }

    /// <summary>Сумма размеров каталогов выбранных репозиториев после последнего успешного бэкапа (байты).</summary>
    public long? LastBackupSelectedTotalBytes { get; set; }

    /// <summary>Размер созданного ZIP после последнего успешного бэкапа (байты), если ZIP включён.</summary>
    public long? LastBackupZipArchiveBytes { get; set; }

    /// <summary>Длительность последнего запуска бэкапа (миллисекунды).</summary>
    public long? LastBackupDurationMs { get; set; }

    public bool ShowOnlySelectedRepositories { get; set; }

    /// <summary>Строка лога/прогресса бекапа скрыта пользователем; сбрасывается при старте бекапа.</summary>
    public bool BackupProgressLogDismissed { get; set; }

    /// <summary>Кэш списка репозиториев с Git API (JSON, camelCase); обновляется при успешном refresh.</summary>
    public string CachedRepositoriesJson { get; set; }

    /// <summary>Время успешного обновления <see cref="CachedRepositoriesJson"/> (UTC).</summary>
    public DateTimeOffset? CachedRepositoriesFetchedUtc { get; set; }
}