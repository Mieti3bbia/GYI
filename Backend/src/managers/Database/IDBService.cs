using FYI.DataModels.Entities;

using GYI.src.enumerations;

namespace FYI.Managers.Database;

public interface IDBService
{
    Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        Guid editor, CancellationToken cancellationToken);
    Task<StoredItinerary?> GetItineraryAsync(Guid editor, Guid id, CancellationToken cancellationToken);
    Task<bool> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> SaveItineraryAsync(Itinerary itinerary, Guid[] stopIds, CancellationToken cancellationToken);
    Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, Guid[] stopIds, CancellationToken cancellationToken);
}

public sealed record StoredItinerary(Itinerary Metadata, IReadOnlyList<Stop> Stops);

public enum ItineraryWriteResult
{
    Saved,
    TitleConflict,
    NotFound,
    UnavailableStops
}
