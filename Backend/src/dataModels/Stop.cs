using System.Text.Json.Nodes;
using GYI.src.dataModels.interfaces;
using System.Globalization;
using GYI.src.enumerations;

namespace GYI.src.dataModels;

public sealed class Stop(FYI.DataModels.Entities.Stop data) : IDataModel
{
    public Guid Id => data.Id;
    public string Name => data.Name;
    public string Description => data.Description;
    public decimal Latitude => data.CoordinateY;
    public decimal Longitude => data.CoordinateX;
    public string? Cap => data.Cap is > 0 and <= 99999 && decimal.Truncate(data.Cap) == data.Cap
        ? data.Cap.ToString("00000", CultureInfo.InvariantCulture)
        : null;
    public decimal Price => data.Price;
    public string? Municipality => data.Municipality;
    public string? Province => data.Province;
    public string? SourceUrl => data.SourceUrl;
    public bool CanAddToItinerary => data.IsFixed && data.Status == nameof(PublicationStatusEnum.Accepted)
        && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180
        && !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Description);

    public JsonObject GetJSONData() => IDataModel.Serialize(this);
}
