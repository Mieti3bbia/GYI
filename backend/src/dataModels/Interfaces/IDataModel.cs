using System.Text.Json;
using System.Text.Json.Nodes;

namespace GYI.src.dataModels.interfaces;

public interface IDataModel
{
    JsonObject GetJSONData();

    string GetJSONDataString() => GetJSONData().ToJsonString();

    internal static JsonObject Serialize<T>(T model) =>
        JsonSerializer.SerializeToNode(model, JsonSerializerOptions.Web)!.AsObject();
}
