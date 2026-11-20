using System.Text.Json.Nodes;
using GYI.src.dataModels.interfaces;

namespace GYI.src.dataModels;

public sealed record Segment(Guid ItineraryId, Guid DepartureStopId, Guid ArrivalStopId, int Order) : IDataModel
{
    public JsonObject GetJSONData() => IDataModel.Serialize(this);
}
