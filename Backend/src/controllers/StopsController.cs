using FYI.Application;
using GYI.src.dataModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FYI.Controllers;

[ApiController]
[Authorize(Roles = SecurityManager.EditorRole)]
[Route("api/stops")]
public sealed class StopsController(Coordinator coordinator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Stop>> Create(DraftStopDetails input, CancellationToken ct)
    {
        StopInputValidation.Validate(ModelState, input);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var stop = await coordinator.CreateCatalogStopAsync(input, ct);
        return stop is null
            ? Problem(statusCode: StatusCodes.Status409Conflict,
                detail: "Una tappa del catalogo ha già lo stesso nome, le stesse coordinate o lo stesso URL della fonte.")
            : StatusCode(StatusCodes.Status201Created, stop);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Stop>>> Get(CancellationToken ct) =>
        Ok(await coordinator.GetStopsAsync(ct));

    [HttpGet("fixed")]
    public async Task<ActionResult<IReadOnlyList<Stop>>> Fixed(CancellationToken ct) =>
        Ok(await coordinator.GetFixedStopsAsync(ct));
}
