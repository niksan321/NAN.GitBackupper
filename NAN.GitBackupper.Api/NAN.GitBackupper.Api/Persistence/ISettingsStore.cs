using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Persistence;

public interface ISettingsStore
{
    Task<SettingsModel> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(SettingsModel settings, CancellationToken ct = default);
}
