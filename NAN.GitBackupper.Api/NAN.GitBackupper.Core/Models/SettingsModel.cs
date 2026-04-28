using System.ComponentModel;

namespace NAN.GitBackupper.Core.Models;

public class SettingsModel
{
    public List<TargetItemModel> TargetItems { get; set; }

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

    /// <summary>Совпадает с NAN.GitBackupper.Api.Models.Enums.SchedulerStartupPreference (0/1/2). Null — в JSON не было поля.</summary>
    public int? SchedulerStartupPreference { get; set; }
}
