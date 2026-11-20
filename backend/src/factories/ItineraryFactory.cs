using GYI.src.dataModels;
using ItineraryEntity = FYI.DataModels.Entities.Itinerary;
using StopEntity = FYI.DataModels.Entities.Stop;

namespace GYI.src.factories;

public sealed class ItineraryFactory
{
    public Itinerary CreateNewItinerary(
        Guid editorId, string title, string theme, string description, string geographicArea, IReadOnlyList<Stop> stops)
    {
        return new Itinerary(new ItineraryEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Theme = theme,
            Description = description,
            GeographicArea = geographicArea,
            Status = ItineraryEntity.DraftStatus,
            IsCustomizable = false,
            CreatedBy = editorId
        }, stops);
    }

    internal Itinerary RestoreItinerary(ItineraryEntity metadata, IEnumerable<StopEntity> stops) =>
        new(metadata, stops.Select(CreateNewStop));

    public Stop CreateNewStop(StopEntity data) => new(data);
}
