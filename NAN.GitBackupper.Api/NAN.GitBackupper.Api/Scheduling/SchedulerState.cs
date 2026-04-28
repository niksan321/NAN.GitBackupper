namespace NAN.GitBackupper.Api.Scheduling;

public sealed class SchedulerState
{
    private volatile bool isRunning;

    public bool IsRunning
    {
        get => isRunning;
        set => isRunning = value;
    }
}
