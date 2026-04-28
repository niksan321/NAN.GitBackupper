using System.ComponentModel;
using NAN.GitBackupper.Api.Models.Enums;
using NAN.GitBackupper.Api.Scheduling;

namespace NAN.GitBackupper.Api.Models;

public class SettingsModel
{
    public List<BackupProfileModel> TargetItems { get; set; }

    /// <summary>Только для ответа GET /api/settings: снимок статуса бекапа по id профиля.</summary>
    public Dictionary<Guid, ProfileBackupStatus> ProfileBackupStatuses { get; set; }

    public bool RunOnStartUp { get; set; }

    public bool AutoStartPing { get; set; }

    public bool Notify { get; set; }

    public bool StartMinimized { get; set; }

    public bool ShowDeletePrompt { get; set; }

    public bool PlayErrorSound { get; set; }

    public string Language { get; set; }

    public int TimeToShowPopupSec { get; set; }

    public int SortingColumn { get; set; }

    public ListSortDirection SortingDirection { get; set; }

    /// <summary>
    /// Автозапуск планировщика при старте API. Null в JSON — до появления поля; трактуется как <see cref="SchedulerStartupPreference.ResumeLastState"/>.
    /// </summary>
    public SchedulerStartupPreference? SchedulerStartupPreference { get; set; }
}
