namespace NAN.GitBackupper.Api.Options;

public sealed class GitBackupperOptions
{
    public const string SectionName = "GitBackupper";

    /// <summary>File name in content root (default settings.json).</summary>
    public string SettingsFileName { get; set; } = "settings.json";

    /// <summary>When true, scheduler starts after host starts (unless stopped via API).</summary>
    public bool SchedulerAutoStart { get; set; }

    /// <summary>Allowed CORS origins for dev (e.g. http://localhost:4200).</summary>
    public string[] CorsAllowedOrigins { get; set; } = [];

    /// <summary>Общий родительский каталог бэкапа для всех профилей (подпапки по имени профиля).</summary>
    public string BackupRootPath { get; set; } = "";

    /// <summary>Путь к файлу SQLite относительно ContentRoot (профили бэкапа).</summary>
    public string SqliteDatabasePath { get; set; } = "data/gitbackupper.db";

    /// <summary>Удалять подкаталоги в BackupRootPath без соответствующего профиля (по расписанию и при старте).</summary>
    public bool OrphanProfileBackupFolderCleanupEnabled { get; set; } = true;

    /// <summary>Интервал между прогонами (часы), не меньше 1.</summary>
    public int OrphanProfileBackupCleanupIntervalHours { get; set; } = 1;
}
