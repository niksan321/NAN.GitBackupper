namespace NAN.GitBackupper.Api.Persistence;

/// <summary>Флаг планировщика после Start/Stop API; null — записи в БД ещё нет (использовать SchedulerAutoStart из конфига).</summary>
public interface ISchedulerRunningPreferenceStore
{
    Task<bool?> GetAsync(CancellationToken ct = default);

    Task SetAsync(bool isRunning, CancellationToken ct = default);
}
