using System.Text.Json.Nodes;
using GYI.src.dataModels.interfaces;
using GYI.src.enumerations;

namespace GYI.src.dataModels;

public sealed class Itinerary : IDataModel
{
    internal Itinerary(FYI.DataModels.Entities.Itinerary metadata, IEnumerable<Stop> stops)
    {
        Metadata = metadata;
        Stops = Array.AsReadOnly(stops.ToArray());
    }

    internal FYI.DataModels.Entities.Itinerary Metadata { get; }
    public Guid Id => Metadata.Id;
    public string Title => Metadata.Title;
    public string? Theme => Metadata.Theme;
    public string Description => Metadata.Description;
    public string? GeographicArea => Metadata.GeographicArea;
    public ItineraryStatus Status => Metadata.Status;
    public IReadOnlyList<Stop> Stops { get; }

    public IReadOnlyList<Segment> GetSegments() =>
        Stops.Zip(Stops.Skip(1), (departure, arrival) => (departure, arrival))
             .Select((pair, index) => new Segment(Id, pair.departure.Id, pair.arrival.Id, index + 1))
             .ToArray();

    public JsonObject GetJSONData() => IDataModel.Serialize(this);
}
