using FYI.Managers.Database;
using GYI.src.dataModels;
using GYI.src.enumerations;
using GYI.src.factories;

namespace FYI.Application;

public sealed class Coordinator(SecurityManager security, ItineraryFactory factory, DBManager database)
{
    public async Task<Stop?> CreateCatalogStopAsync(DraftStopDetails details, CancellationToken ct)
    {
        var stop = await database.CreateCatalogStopAsync(security.AuthorizeEditor(), details, ct);
        return stop is null ? null : factory.CreateNewStop(stop);
    }

    public async Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken ct)
    {
        security.AuthorizeEditor();
        var stops = await database.GetStopsAsync(ct);
        return stops.Select(factory.CreateNewStop).ToArray();
    }

    public async Task<IReadOnlyList<Stop>> GetFixedStopsAsync(CancellationToken ct)
    {
        security.AuthorizeEditor();
        var stops = await database.GetFixedStopsAsync(null, ct);
        return stops.Select(factory.CreateNewStop).ToArray();
    }

    public Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        CancellationToken ct) => database.GetItinerariesAsync(security.AuthorizeEditor(), ct);

    public Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetPublicItinerariesAsync(
        CancellationToken ct) => database.GetItinerariesAsync(null, ct);

    public Task<ItineraryWriteResult> ConfirmItineraryAsync(Guid id, CancellationToken ct) =>
        database.ConfirmItineraryAsync(security.AuthorizeEditor(), id, ct);

    public Task<bool> UpdateParticipationAsync(Guid id, bool accepts, decimal? price, CancellationToken ct) =>
        database.UpdateParticipationAsync(security.AuthorizeEditor(), id, accepts, price, ct);

    public Task<Itinerary?> RetrieveItineraryAsync(Guid id, CancellationToken ct) =>
        RetrieveItineraryAsync(security.AuthorizeEditor(), id, ct);

    public Task<Itinerary?> RetrievePublicItineraryAsync(Guid id, CancellationToken ct) =>
        RetrieveItineraryAsync(null, id, ct);

    private async Task<Itinerary?> RetrieveItineraryAsync(Guid? editor, Guid id, CancellationToken ct)
    {
        var result = await database.RetrieveItineraryAsync(editor, id, ct);
        return result is null ? null : factory.RestoreItinerary(result.Metadata, result.Stops);
    }

    public async Task<Itinerary?> CreateItineraryAsync(DraftChanges changes, CancellationToken ct)
    {
        var editor = security.AuthorizeEditor();
        ValidateDistinctStops(ExistingIds(changes.Stops));
        var itinerary = factory.CreateNewItinerary(editor, changes.Title, changes.Theme,
            changes.Description, changes.GeographicArea, []);
        var result = await database.SaveItineraryAsync(itinerary, changes.Stops, ct);
        EnsureStopsAvailable(result);
        return result == ItineraryWriteResult.Saved ? itinerary : null;
    }

    public async Task<bool> UpdateItineraryStopsAsync(Guid id, Guid[] stopIds, CancellationToken ct)
    {
        var editor = security.AuthorizeEditor();
        ValidateDistinctStops(stopIds);
        var result = await database.UpdateItineraryStopsAsync(editor, id, stopIds, ct);
        EnsureStopsAvailable(result);
        return result == ItineraryWriteResult.Saved;
    }

    public async Task<ItineraryWriteResult> UpdateItineraryAsync(Guid id, DraftChanges changes, CancellationToken ct)
    {
        var editor = security.AuthorizeEditor();
        ValidateDistinctStops(ExistingIds(changes.Stops));
        var result = await database.UpdateItineraryAsync(editor, id, changes, ct);
        EnsureStopsAvailable(result);
        return result;
    }

    private static Guid[] ExistingIds(DraftStopChange[] stops) =>
        stops.Where(stop => stop.StopId.HasValue).Select(stop => stop.StopId!.Value).ToArray();

    private static void ValidateDistinctStops(Guid[] ids)
    {
        if (ids.Distinct().Count() != ids.Length)
        {
            throw new StopSelectionException("Scegli tappe fisse distinte.");
        }
    }

    private static void EnsureStopsAvailable(ItineraryWriteResult result)
    {
        if (result == ItineraryWriteResult.UnavailableStops)
        {
            throw new StopSelectionException("Una o più tappe fisse non sono disponibili.");
        }
        if (result == ItineraryWriteResult.DuplicateStops)
        {
            throw new StopSelectionException("Scegli tappe fisse distinte.");
        }
    }
}

public sealed class StopSelectionException(string message) : Exception(message);
