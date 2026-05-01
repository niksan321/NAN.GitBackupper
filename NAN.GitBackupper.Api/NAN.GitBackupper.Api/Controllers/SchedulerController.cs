using Microsoft.AspNetCore.Mvc;
using NAN.GitBackupper.Api.Scheduling;

namespace NAN.GitBackupper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SchedulerController(GitSchedulerCoordinator coordinator, SchedulerState schedulerState) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<SchedulerStatusDto> Status() =>
        Ok(new SchedulerStatusDto { IsRunning = schedulerState.IsRunning });

    [HttpPost("start")]
    public async Task<ActionResult<SchedulerStatusDto>> Start(CancellationToken ct)
    {
        await coordinator.StartAsync(ct);
        return Ok(new SchedulerStatusDto { IsRunning = true });
    }

    [HttpPost("stop")]
    public async Task<ActionResult<SchedulerStatusDto>> Stop(CancellationToken ct)
    {
        await coordinator.StopAsync(ct);
        return Ok(new SchedulerStatusDto { IsRunning = false });
    }
}

public sealed class SchedulerStatusDto
{
    public bool IsRunning { get; set; }
}