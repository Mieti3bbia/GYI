using FYI.Application;
using GYI.src.dataModels;
using GYI.src.enumerations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FYI.Managers.Database;
using System.ComponentModel.DataAnnotations;

namespace FYI.Controllers;

[ApiController]
[Authorize(Roles = SecurityManager.EditorRole)]
[Route("api/itineraries")]
public sealed class ItinerariesController(Coordinator coordinator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ItinerarySummary>> Create(CreateDraft input, CancellationToken ct)
    {
        ValidateItinerary(input.Title, input.Theme, input.Description, input.GeographicArea);
        if (input.Stops is not null && input.StopIds is not null)
            ModelState.AddModelError("stops", "Invia una sola selezione di tappe.");
        var stops = input.Stops ?? (input.StopIds ?? []).Select(id => new DraftStopChange(id, null)).ToArray();
        ValidateSelection(stops);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var itinerary = await coordinator.CreateItineraryAsync(
                ToChanges(input.Title, input.Theme, input.Description, input.GeographicArea, stops), ct);
            if (itinerary is null)
            {
                return TitleConflict();
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
    public async Task<ActionResult<ItineraryDetail>> Detail(Guid id, CancellationToken ct) =>
        ItineraryResponse(await coordinator.RetrieveItineraryAsync(id, ct));

    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<IReadOnlyList<ItinerarySummary>>> Public(CancellationToken ct)
    {
        var itineraries = await coordinator.GetPublicItinerariesAsync(ct);
        return Ok(itineraries.Select(item => new ItinerarySummary(item.Id, item.Title, item.Status)));
    }

    [AllowAnonymous]
    [HttpGet("public/{id:guid}")]
    public async Task<ActionResult<ItineraryDetail>> PublicDetail(Guid id, CancellationToken ct) =>
        ItineraryResponse(await coordinator.RetrievePublicItineraryAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateDraft input, CancellationToken ct)
    {
        ValidateItinerary(input.Title, input.Theme, input.Description, input.GeographicArea);
        ValidateSelection(input.Stops);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var changes = ToChanges(input.Title, input.Theme, input.Description, input.GeographicArea, input.Stops);
            return await coordinator.UpdateItineraryAsync(id, changes, ct) switch
            {
                ItineraryWriteResult.Saved => NoContent(),
                ItineraryWriteResult.TitleConflict => TitleConflict(),
                _ => NotFound()
            };
        }
        catch (StopSelectionException exception)
        {
            return InvalidStops(exception);
        }
    }

    [HttpPut("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        await coordinator.ConfirmItineraryAsync(id, ct) switch
        {
            ItineraryWriteResult.Saved => NoContent(),
            ItineraryWriteResult.IncompleteItinerary or ItineraryWriteResult.UnavailableStops =>
                Problem(statusCode: StatusCodes.Status409Conflict,
                    detail: "Per pubblicare servono titolo, tema, descrizione e almeno due tappe fisse valide e distinte."),
            _ => NotFound()
        };

    [HttpPut("{id:guid}/participation")]
    public async Task<IActionResult> UpdateParticipation(Guid id, ParticipationInput input, CancellationToken ct)
    {
        var accepts = input.AcceptsEconomicStops!.Value;
        var price = accepts ? input.AverageTripPrice : null;
        if (accepts && (price is null || price < 0 || price > 9999999.99m || Math.Round(price.Value, 2) != price.Value))
        {
            ModelState.AddModelError("averageTripPrice", "Inserisci un prezzo in euro tra 0 e 9.999.999,99 con al massimo due decimali.");
            return ValidationProblem(ModelState);
        }

        return await coordinator.UpdateParticipationAsync(id, accepts, price, ct) ? NoContent() : NotFound();
    }

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

    private ActionResult<ItineraryDetail> ItineraryResponse(Itinerary? itinerary) =>
        itinerary is null
            ? NotFound()
            : new ItineraryDetail(
                itinerary.Id, itinerary.Title, itinerary.Theme, itinerary.Description, itinerary.GeographicArea,
                itinerary.Status, itinerary.Stops, itinerary.GetSegments(), itinerary.AcceptsEconomicStops,
                itinerary.AverageTripPrice);

    private ActionResult TitleConflict()
    {
        ModelState.AddModelError("title", "Esiste già un itinerario con questo titolo.");
        return ValidationProblem(statusCode: StatusCodes.Status409Conflict, modelStateDictionary: ModelState);
    }

    private void ValidateItinerary(string? title, string? theme, string? description, string? geographicArea)
    {
        StopInputValidation.ValidateText(ModelState, "title", title);
        StopInputValidation.ValidateText(ModelState, "theme", theme);
        StopInputValidation.ValidateText(ModelState, "description", description);
        StopInputValidation.ValidateText(ModelState, "geographicArea", geographicArea);
    }

    private void ValidateSelection(DraftStopChange?[] stops)
    {
        for (var index = 0; index < stops.Length; index++)
        {
            var stop = stops[index];
            var field = $"stops[{index}]";
            if (stop is null || (stop.StopId is null && stop.Details is null))
                ModelState.AddModelError(field, "Seleziona una tappa o inserisci i suoi dati.");
            else if (stop.Details is { } details)
                StopInputValidation.Validate(ModelState, details, field + ".");
        }
    }

    private static DraftChanges ToChanges(string? title, string? theme, string? description, string? area,
        DraftStopChange?[] stops) => new(title!, theme!, description!, area!, stops.Select(stop => stop!).ToArray());
}

public sealed record CreateDraft(string? Title, string? Theme, string? Description, string? GeographicArea,
    Guid[]? StopIds, DraftStopChange?[]? Stops);
public sealed record UpdateDraft(string? Title, string? Theme, string? Description, string? GeographicArea,
    [Required] DraftStopChange?[] Stops);
public sealed record UpdateDraftStops([Required] Guid[] StopIds);
public sealed record ParticipationInput([Required] bool? AcceptsEconomicStops, decimal? AverageTripPrice);
public sealed record ItinerarySummary(Guid Id, string Title, ItineraryStatus Status);
public sealed record ItineraryDetail(
    Guid Id, string Title, string? Theme, string Description, string? GeographicArea,
    ItineraryStatus Status, IReadOnlyList<Stop> Stops, IReadOnlyList<Segment> Segments,
    bool AcceptsEconomicStops, decimal? AverageTripPrice);
