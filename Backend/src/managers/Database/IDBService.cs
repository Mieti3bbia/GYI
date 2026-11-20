using FYI.DataModels.Entities;

using GYI.src.enumerations;
using DraftChanges = GYI.src.dataModels.DraftChanges;
using DraftStopChange = GYI.src.dataModels.DraftStopChange;
using DraftStopDetails = GYI.src.dataModels.DraftStopDetails;

namespace FYI.Managers.Database;

public interface IDBService
{
    Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken cancellationToken);
    Task<Stop?> CreateCatalogStopAsync(Guid editor, DraftStopDetails details, CancellationToken cancellationToken);
    Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        Guid? editor, CancellationToken cancellationToken);
    Task<StoredItinerary?> GetItineraryAsync(Guid? editor, Guid id, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken cancellationToken);
    Task<bool> UpdateParticipationAsync(Guid editor, Guid id, bool accepts, decimal? price, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> SaveItineraryAsync(
        Itinerary itinerary, DraftStopChange[] stops, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, Guid[] stopIds, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> UpdateItineraryAsync(
        Guid editor, Guid id, DraftChanges changes, CancellationToken cancellationToken);
}

public sealed record StoredItinerary(Itinerary Metadata, IReadOnlyList<Stop> Stops);

public enum ItineraryWriteResult
{
    Saved,
    TitleConflict,
    NotFound,
    UnavailableStops,
    DuplicateStops,
    IncompleteItinerary
}
