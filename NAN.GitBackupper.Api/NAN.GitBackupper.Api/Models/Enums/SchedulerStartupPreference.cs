namespace NAN.GitBackupper.Api.Models.Enums;

/// <summary>Правило автозапуска Quartz-планировщика при старте процесса API.</summary>
public enum SchedulerStartupPreference
{
    /// <summary>Всегда запускать расписание (ResumeAll).</summary>
    AlwaysStart = 0,

    /// <summary>Никогда не запускать автоматически (PauseAll).</summary>
    NeverStart = 1,

    /// <summary>Как при последнем явном Start/Stop через API; если записи в БД ещё нет — как SchedulerAutoStart в конфиге.</summary>
    ResumeLastState = 2,
}
