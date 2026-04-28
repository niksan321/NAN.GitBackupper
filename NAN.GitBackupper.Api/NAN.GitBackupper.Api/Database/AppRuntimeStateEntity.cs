namespace NAN.GitBackupper.Api.Database;

/// <summary>Одна строка (Id = <see cref="SingletonId"/>): сохранённое из API состояние «планировщик запущен».</summary>
public sealed class AppRuntimeStateEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    /// <summary>Quartz: профильные джобы в режиме resume (не PauseAll).</summary>
    public bool IsSchedulerRunning { get; set; }
}
