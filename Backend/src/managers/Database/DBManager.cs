using Itinerary = GYI.src.dataModels.Itinerary;
using Stop = FYI.DataModels.Entities.Stop;
using GYI.src.enumerations;

namespace FYI.Managers.Database;

public sealed class DBManager(IDBService service)
{
    public Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken ct) => service.GetStopsAsync(ct);

    public Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken ct) =>
        service.GetFixedStopsAsync(ids, ct);

    public Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        Guid editor, CancellationToken ct) => service.GetItinerariesAsync(editor, ct);

    public Task<StoredItinerary?> RetrieveItineraryAsync(Guid editor, Guid id, CancellationToken ct) =>
        service.GetItineraryAsync(editor, id, ct);

    public Task<bool> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken ct) =>
        service.ConfirmItineraryAsync(editor, id, ct);

    public Task<ItineraryWriteResult> SaveItineraryAsync(Itinerary itinerary, CancellationToken ct) =>
        service.SaveItineraryAsync(itinerary.Metadata, itinerary.Stops.Select(stop => stop.Id).ToArray(), ct);

    public Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, IReadOnlyList<GYI.src.dataModels.Stop> stops, CancellationToken ct) =>
        service.UpdateItineraryStopsAsync(editor, id, stops.Select(stop => stop.Id).ToArray(), ct);
}
