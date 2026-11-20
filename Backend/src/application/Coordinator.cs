using FYI.Managers.Database;
using GYI.src.dataModels;
using GYI.src.enumerations;
using GYI.src.factories;

namespace FYI.Application;

public sealed class Coordinator(SecurityManager security, ItineraryFactory factory, DBManager database)
{
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

    public Task<bool> ConfirmItineraryAsync(Guid id, CancellationToken ct) =>
        database.ConfirmItineraryAsync(security.AuthorizeEditor(), id, ct);

    public async Task<Itinerary?> RetrieveItineraryAsync(Guid id, CancellationToken ct)
    {
        var result = await database.RetrieveItineraryAsync(security.AuthorizeEditor(), id, ct);
        return result is null ? null : factory.RestoreItinerary(result.Metadata, result.Stops);
    }

    public async Task<Itinerary?> CreateItineraryAsync(
        string title, string theme, string description, string geographicArea, Guid[]? stopIds, CancellationToken ct)
    {
        var editor = security.AuthorizeEditor();
        var stops = await ResolveStopsAsync(stopIds, ct);
        var itinerary = factory.CreateNewItinerary(editor, title, theme, description, geographicArea, stops);
        var result = await database.SaveItineraryAsync(itinerary, ct);
        EnsureStopsAvailable(result);
        return result == ItineraryWriteResult.Saved ? itinerary : null;
    }

    public async Task<bool> UpdateItineraryStopsAsync(Guid id, Guid[] stopIds, CancellationToken ct)
    {
        var editor = security.AuthorizeEditor();
        var stops = await ResolveStopsAsync(stopIds, ct);
        var result = await database.UpdateItineraryStopsAsync(editor, id, stops, ct);
        EnsureStopsAvailable(result);
        return result == ItineraryWriteResult.Saved;
    }

    private async Task<IReadOnlyList<Stop>> ResolveStopsAsync(Guid[]? ids, CancellationToken ct)
    {
        if (ids is null || ids.Length == 0)
        {
            return [];
        }
        if (ids.Distinct().Count() != ids.Length)
        {
            throw new StopSelectionException("Scegli tappe fisse distinte.");
        }

        var rows = await database.GetFixedStopsAsync(ids, ct);
        if (rows.Count != ids.Length)
        {
            throw new StopSelectionException("Una o più tappe fisse non sono disponibili.");
        }

        var stops = rows.ToDictionary(stop => stop.Id, factory.CreateNewStop);
        return ids.Select(id => stops[id]).ToArray();
    }

    private static void EnsureStopsAvailable(ItineraryWriteResult result)
    {
        if (result == ItineraryWriteResult.UnavailableStops)
        {
            throw new StopSelectionException("Una o più tappe fisse non sono disponibili.");
        }
    }
}

public sealed class StopSelectionException(string message) : Exception(message);
