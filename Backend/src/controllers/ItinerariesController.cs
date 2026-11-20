using FYI.Application;
using GYI.src.dataModels;
using GYI.src.enumerations;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FYI.Controllers;

[ApiController]
[Route("api/itineraries")]
public sealed class ItinerariesController(Coordinator coordinator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ItinerarySummary>> Create(CreateDraft input, CancellationToken ct)
    {
        ValidateField("title", input.Title);
        ValidateField("theme", input.Theme);
        ValidateField("description", input.Description);
        ValidateField("geographicArea", input.GeographicArea);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var itinerary = await coordinator.CreateItineraryAsync(
                input.Title!, input.Theme!, input.Description!, input.GeographicArea!, input.StopIds, ct);
            if (itinerary is null)
            {
                ModelState.AddModelError("title", "Esiste già un itinerario con questo titolo.");
                return ValidationProblem(statusCode: StatusCodes.Status409Conflict, modelStateDictionary: ModelState);
            }
            return CreatedAtAction(nameof(Detail), new { id = itinerary.Id },
                new ItinerarySummary(itinerary.Id, itinerary.Title, itinerary.Status));
        }
        catch (StopSelectionException exception)
        {
            return InvalidStops(exception);
        }
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ItinerarySummary>>> Mine(CancellationToken ct)
    {
        var itineraries = await coordinator.GetItinerariesAsync(ct);
        return Ok(itineraries.Select(item => new ItinerarySummary(item.Id, item.Title, item.Status)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItineraryDetail>> Detail(Guid id, CancellationToken ct)
    {
        var draft = await coordinator.RetrieveItineraryAsync(id, ct);
        if (draft is null)
        {
            return NotFound();
        }
        return new ItineraryDetail(
            draft.Id, draft.Title, draft.Theme, draft.Description, draft.GeographicArea,
            draft.Status, draft.Stops, draft.GetSegments());
    }

    [HttpPut("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        await coordinator.ConfirmItineraryAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/stops")]
    public async Task<IActionResult> UpdateStops(Guid id, UpdateDraftStops input, CancellationToken ct)
    {
        try
        {
            return await coordinator.UpdateItineraryStopsAsync(id, input.StopIds, ct) ? NoContent() : NotFound();
        }
        catch (StopSelectionException exception)
        {
            return InvalidStops(exception);
        }
    }

    private ActionResult InvalidStops(StopSelectionException exception)
    {
        ModelState.AddModelError("stopIds", exception.Message);
        return ValidationProblem(ModelState);
    }

    private void ValidateField(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ModelState.AddModelError(field, "Campo obbligatorio.");
        }
        else if (value.Contains('\0'))
        {
            ModelState.AddModelError(field, "Il campo contiene un carattere non valido.");
        }
    }
}

public sealed record CreateDraft(string? Title, string? Theme, string? Description, string? GeographicArea, Guid[]? StopIds);
public sealed record UpdateDraftStops([Required] Guid[] StopIds);
public sealed record ItinerarySummary(Guid Id, string Title, ItineraryStatus Status);
public sealed record ItineraryDetail(
    Guid Id, string Title, string? Theme, string Description, string? GeographicArea,
    ItineraryStatus Status, IReadOnlyList<Stop> Stops, IReadOnlyList<Segment> Segments);
