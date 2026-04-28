using System.ComponentModel;
using NAN.GitBackupper.Core.Enums;

namespace NAN.GitBackupper.Core.Models;

public class TargetItemModel
{
    public Guid Id { get; set; }

    public GitProviderType Provider { get; set; }

    /// <summary>HTTPS clone operations and API token (PAT or app password).</summary>
    public string ApiKey { get; set; }

    /// <summary>Bitbucket: Atlassian account email (API token) or Bitbucket username (app password).</summary>
    public string ProviderUserName { get; set; }

    /// <summary>GitLab / Other: instance base URL without trailing path, e.g. https://gitlab.company.com</summary>
    public string GitLabBaseUrl { get; set; }

    /// <summary>Родительский каталог бекапа; клоны и ZIP лежат в подпапке с именем по Id профиля (см. TargetItemBackupPaths.GetProfileDataFolderName).</summary>
    public string BackupRootPath { get; set; }

    public string Name { get; set; }

    public TimePeriod BackupInterval { get; set; } = new(TimePeriodType.Hours, 24);

    public TimePeriod OperationTimeout { get; set; } = new(TimePeriodType.Minutes, 2);

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

    /// <summary>На экране списка репозиториев: показывать только включённые в бэкап.</summary>
    public bool ShowOnlySelectedRepositories { get; set; }
}
