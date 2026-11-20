using System.Text.Json.Serialization;

namespace GYI.src.enumerations;

[JsonConverter(typeof(JsonStringEnumConverter<ItineraryStatus>))]
public enum ItineraryStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("confirmed")]
    Confirmed
}
