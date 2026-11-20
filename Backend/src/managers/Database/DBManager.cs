using Itinerary = GYI.src.dataModels.Itinerary;
using Stop = FYI.DataModels.Entities.Stop;
using GYI.src.enumerations;
using DraftChanges = GYI.src.dataModels.DraftChanges;
using DraftStopChange = GYI.src.dataModels.DraftStopChange;
using DraftStopDetails = GYI.src.dataModels.DraftStopDetails;

namespace FYI.Managers.Database;

public sealed class DBManager(IDBService service)
{
    public Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken ct) => service.GetStopsAsync(ct);
    public Task<Stop?> CreateCatalogStopAsync(Guid editor, DraftStopDetails details, CancellationToken ct) =>
        service.CreateCatalogStopAsync(editor, details, ct);

    public Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken ct) =>
        service.GetFixedStopsAsync(ids, ct);

    public Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        Guid? editor, CancellationToken ct) => service.GetItinerariesAsync(editor, ct);

    public Task<StoredItinerary?> RetrieveItineraryAsync(Guid? editor, Guid id, CancellationToken ct) =>
        service.GetItineraryAsync(editor, id, ct);

    public Task<ItineraryWriteResult> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken ct) =>
        service.ConfirmItineraryAsync(editor, id, ct);

    public Task<bool> UpdateParticipationAsync(Guid editor, Guid id, bool accepts, decimal? price, CancellationToken ct) =>
        service.UpdateParticipationAsync(editor, id, accepts, price, ct);

    public Task<ItineraryWriteResult> SaveItineraryAsync(Itinerary itinerary, DraftStopChange[] stops, CancellationToken ct) =>
        service.SaveItineraryAsync(itinerary.Metadata, stops, ct);

    public Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, Guid[] stopIds, CancellationToken ct) =>
        service.UpdateItineraryStopsAsync(editor, id, stopIds, ct);

    public Task<ItineraryWriteResult> UpdateItineraryAsync(
        Guid editor, Guid id, DraftChanges changes, CancellationToken ct) =>
        service.UpdateItineraryAsync(editor, id, changes, ct);
}
