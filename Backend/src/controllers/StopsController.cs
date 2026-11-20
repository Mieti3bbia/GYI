using FYI.Application;
using GYI.src.dataModels;
using Microsoft.AspNetCore.Mvc;

namespace FYI.Controllers;

[ApiController]
[Route("api/stops")]
public sealed class StopsController(Coordinator coordinator) : ControllerBase
{
    [HttpGet("fixed")]
    public async Task<ActionResult<IReadOnlyList<Stop>>> Fixed(CancellationToken ct) =>
        Ok(await coordinator.GetFixedStopsAsync(ct));
}
