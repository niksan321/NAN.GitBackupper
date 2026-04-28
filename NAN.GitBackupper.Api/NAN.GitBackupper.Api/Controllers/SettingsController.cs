using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Persistence;
using NAN.GitBackupper.Api.Scheduling;
using NAN.GitBackupper.Api.Services;
using NAN.GitBackupper.Api.Models;
using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SettingsController(
    ISettingsStore settingsStore,
    GitSchedulerCoordinator schedulerCoordinator,
    ProfileBackupStatusStore statusStore,
    IBackupProgressLogDismissedStore progressLogDismissedStore,
    IOptions<GitBackupperOptions> optionsAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsModel>> Get([FromQuery] string? sort, [FromQuery] string? order,
        CancellationToken ct)
    {
        var s = await settingsStore.LoadAsync(ct);
        ProfileBackupRootHelper.ApplyConfiguredRootToAll(s, optionsAccessor.Value);
        TableQuerySort.SortTargetItems(s.TargetItems, sort, order);
        if (s.TargetItems is { Count: > 0 })
        {
            var statuses = new Dictionary<Guid, ProfileBackupStatus>();
            foreach (var p in s.TargetItems)
            {
                var dismissed = await progressLogDismissedStore.GetAsync(p.Id, ct);
                var live = statusStore.GetOrDefault(p.Id);
                statuses[p.Id] = new ProfileBackupStatus
                {
                    IsRunning = live.IsRunning,
                    LastMessage = live.LastMessage ?? "",
                    LastUpdatedUtc = live.LastUpdatedUtc,
                    ProgressValue = live.ProgressValue,
                    ProgressMaximum = live.ProgressMaximum,
                    BackupProgressLogDismissed = dismissed,
                };
            }

            s.ProfileBackupStatuses = statuses;
        }
        else
            s.ProfileBackupStatuses = [];

        return Ok(s);
    }

    [HttpPut]
    public async Task<ActionResult<SettingsModel>> Put([FromBody] SettingsModel model, CancellationToken ct)
    {
        if (model?.TargetItems == null)
            model = new SettingsModel { TargetItems = [] };

        var opt = optionsAccessor.Value;
        if (!ProfileBackupRootHelper.IsRootConfigured(opt))
            return Problem(detail: "Укажите непустой GitBackupper:BackupRootPath в appsettings.json.", statusCode: 400);

        foreach (var t in model.TargetItems)
        {
            t.CoalesceOperationTimeoutFromLegacy();
            if (t.OperationTimeoutSeconds < 15)
                return Problem(detail: "Таймаут операций (HTTP) не может быть меньше 15 секунд.", statusCode: 400);
            if (t.OperationTimeoutSeconds > 86400)
                return Problem(detail: "Таймаут операций (HTTP) не может быть больше суток (86400 сек).", statusCode: 400);
            if (string.IsNullOrWhiteSpace((t.Name ?? "").Trim()))
                return Problem(detail: "Имя профиля не может быть пустым.", statusCode: 400);
            if (!t.KeepArchivesUntilDiskLimit && t.ZipArchivesToKeep < 1)
                return Problem(detail: "Количество ZIP-архивов должно быть не меньше 1.", statusCode: 400);
        }

        if (ProfileBackupRootHelper.HasDuplicateProfileNames(model.TargetItems, out var dup))
            return Problem(detail: $"Профиль с именем «{dup}» уже существует. Имена не должны совпадать.", statusCode: 400);

        if (model.SchedulerStartupPreference is { } pref
            && !Enum.IsDefined(typeof(SchedulerStartupPreference), pref))
            return Problem(detail: "Недопустимое значение SchedulerStartupPreference (ожидается 0, 1 или 2).", statusCode: 400);

        ProfileBackupRootHelper.ApplyConfiguredRootToAll(model, opt);

        await settingsStore.SaveAsync(model, ct);
        await schedulerCoordinator.OnSettingsSavedAsync(model, ct);
        model.ProfileBackupStatuses = null;
        return Ok(model);
    }
}
