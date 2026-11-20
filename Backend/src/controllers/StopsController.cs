using FYI.Application;
using GYI.src.dataModels;
using Microsoft.AspNetCore.Mvc;

namespace FYI.Controllers;

[ApiController]
[Route("api/stops")]
public sealed class StopsController(Coordinator coordinator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Stop>>> Get(CancellationToken ct) =>
        Ok(await coordinator.GetStopsAsync(ct));

    [HttpGet("fixed")]
    public async Task<ActionResult<IReadOnlyList<Stop>>> Fixed(CancellationToken ct) =>
        Ok(await coordinator.GetFixedStopsAsync(ct));
}
