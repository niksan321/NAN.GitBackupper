using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Scheduling;

namespace NAN.GitBackupper.Api.Hosting;

public sealed class SchedulerInitializationHostedService(
    GitSchedulerCoordinator coordinator,
    ISettingsStore settingsStore) : IHostedService
{
    public async Task StartAsync(CancellationToken ct) =>
        await coordinator.ApplyInitialHostStateAsync(settingsStore, ct);

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
