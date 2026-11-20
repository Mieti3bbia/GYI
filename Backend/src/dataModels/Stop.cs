using System.Text.Json.Nodes;
using GYI.src.dataModels.interfaces;
using GYI.src.enumerations;

namespace GYI.src.dataModels;

public sealed class Stop(FYI.DataModels.Entities.Stop data) : IDataModel
{
    public Guid Id => data.Id;
    public string Name => data.Name;
    public string Description => data.Description;
    public decimal Latitude => data.CoordinateY;
    public decimal Longitude => data.CoordinateX;
    public string? Municipality => data.Municipality;
    public string? Province => data.Province;
    public string? SourceUrl => data.SourceUrl;
    public bool CanAddToItinerary => data.IsFixed && data.Status == nameof(PublicationStatusEnum.Accepted);

    public JsonObject GetJSONData() => IDataModel.Serialize(this);
}
