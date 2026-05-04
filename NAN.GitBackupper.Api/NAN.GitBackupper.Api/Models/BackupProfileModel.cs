using System.Text.Json.Serialization;
using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Models;

/// <summary>Профиль бэкапа (модель API, независима от WPF/Core).</summary>
public class BackupProfileModel
{
    public Guid Id { get; set; }

    public GitProviderType Provider { get; set; }

    /// <summary>HTTPS clone operations and API token (PAT or app password).</summary>
    public string ApiKey { get; set; }

    /// <summary>Bitbucket: Atlassian account email (API token) or Bitbucket username (app password).</summary>
    public string ProviderUserName { get; set; }

    /// <summary>GitLab / Other: instance base URL without trailing path, e.g. https://gitlab.company.com</summary>
    public string GitLabBaseUrl { get; set; }

    /// <summary>Родительский каталог бекапа; клоны и ZIP лежат в подпапке с именем по Id профиля (см. BackupPaths.GetProfileDataFolderName).</summary>
    public string BackupRootPath { get; set; }

    public string Name { get; set; }

    public TimePeriod BackupInterval { get; set; } = new(TimePeriodType.Hours, 24);

    /// <summary>Таймаут HTTP-операций (секунды), в БД — одно поле. В JSON при отсутствии ключа — 0 до coalesce.</summary>
    public int OperationTimeoutSeconds { get; set; }

    /// <summary>
    /// Старый формат таймаута в JSON (<c>operationTimeout</c> / <c>OperationTimeout</c> при case-insensitive десериализации).
    /// </summary>
    [JsonPropertyName("operationTimeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TimePeriod OperationTimeoutLegacy { get; set; }

    /// <summary>
    /// Подставляет секунды из legacy только если клиент не передал валидный таймаут (меньше минимума API).
    /// Иначе устаревшее поле <c>operationTimeout</c> игнорируется, чтобы не затирать <c>operationTimeoutSeconds</c>.
    /// </summary>
    public void CoalesceOperationTimeoutFromLegacy()
    {
        const int minApiSeconds = 15;
        var leg = OperationTimeoutLegacy;
        if (leg != null && OperationTimeoutSeconds < minApiSeconds)
        {
            var sec = (int)Math.Ceiling(leg.ToTimeSpan().TotalSeconds);
            if (sec < 1) sec = 1;
            OperationTimeoutSeconds = sec;
        }

        if (OperationTimeoutSeconds == 0)
            OperationTimeoutSeconds = 120;

        OperationTimeoutLegacy = null;
    }

    public bool Enabled { get; set; }

    /// <summary>Удалять в каталоге бекапа подпапки, не относящиеся к выбранным репозиториям, перед синхронизацией.</summary>
    public bool CleanBackupDirectory { get; set; } = true;

    /// <summary>Создавать ZIP всего каталога бекапа после завершения прохода по репозиториям.</summary>
    public bool ZipAfterBackup { get; set; } = true;

    /// <summary>Уровень сжатия ZIP при включённом <see cref="ZipAfterBackup"/>.</summary>
    public ZipCompressionPreset ZipCompression { get; set; } = ZipCompressionPreset.Optimal;

    /// <summary>
    /// Сколько ZIP-файлов хранить в подпапке Archives при включённом <see cref="ZipAfterBackup"/>; лишние удаляются (самые старые).
    /// </summary>
    public int ZipArchivesToKeep { get; set; } = 10;

    /// <summary>
    /// Режим хранения архивов: если true, удалять старые ZIP при превышении лимита по проценту занятого места диска.
    /// Если false, используется <see cref="ZipArchivesToKeep"/>.
    /// </summary>
    public bool KeepArchivesUntilDiskLimit { get; set; }

    /// <summary>Лимит занятого места диска (в процентах) для режима <see cref="KeepArchivesUntilDiskLimit"/>.</summary>
    public int DiskUsageLimitPercent { get; set; } = 80;

    /// <summary>Сколько репозиториев бекапить одновременно (1…число логических процессоров).</summary>
    public int RepositoryBackupParallelism { get; set; } = 2;

    /// <summary>
    /// Ветки для бэкапа по репозиториям: ключ — идентификатор репозитория из API (DisplayKey, например owner/repo);
    /// значение — имя ветки в этом репозитории, которую нужно бэкапить.
    /// </summary>
    public Dictionary<string, string> RepositoryBranches { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool? UseAllRepositories { get; set; }

    public RepositoryBackupSelectionMode? RepositoryBackupSelectionMode { get; set; }

    public List<string> BackupRepositoryKeys { get; set; } = [];

    /// <summary>Размер последнего локального бэкапа по ключу DisplayKey (байты).</summary>
    public Dictionary<string, long> RepositoryBackupSizesBytes { get; set; } =
        new Dictionary<string, long>(StringComparer.Ordinal);

    /// <summary>Сумма размеров выбранных репозиториев после последнего успешного бэкапа (байты).</summary>
    public long? LastBackupSelectedTotalBytes { get; set; }

    /// <summary>Размер ZIP-архива последнего успешного бэкапа (байты), если ZIP создавался.</summary>
    public long? LastBackupZipArchiveBytes { get; set; }

    /// <summary>Длительность последнего запуска бэкапа (миллисекунды).</summary>
    public long? LastBackupDurationMs { get; set; }

    /// <summary>На экране списка репозиториев: показывать только включённые в бэкап.</summary>
    public bool ShowOnlySelectedRepositories { get; set; }
}