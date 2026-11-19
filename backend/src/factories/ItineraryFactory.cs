using System.Text.Json.Nodes;
using GYI.src.enumerations;
using GYI.src.dataModels;

namespace GYI.src.factories
{
    public class ItineraryFactory
    {
        /// <summary>
        /// Creates an empty Itinerary object from a JSON representation of the itinerary data.
        ///     The generated Itinerary object will be empty because the stops list is not provided in the JSON data;
        ///         the stops belonging to the itinerary are indicated by the intermediate table "itinerary_stops".
        /// </summary>
        /// <param name="json">The Json representing the itinerary data and metadata.</param>
        /// <returns>The generated primary object.</returns>
        public static Itinerary CreateEmptyItineraryFromJSON(JsonObject json)
        {            
            int id = json["id"]?.GetValue<int?>() ?? -1;
            string title = json["title"]?.GetValue<string?>() ?? "Unnamed Itinerary";
            string description = json["description"]?.GetValue<string?>() ?? "Missing description.";
            PublicationStatusEnum status = json["status"]?.GetValue<PublicationStatusEnum?>() 
                                            ?? PublicationStatusEnum.Unknown;
            string created_by = json["created_by"]?.GetValue<string?>() ?? "Unknown sponsorship";
            string updated_by = json["updated_by"]?.GetValue<string?>() ?? "Unknown publisher";
            DateTime created_at = json["created_at"]?.GetValue<DateTime?>() ?? new DateTime();
            DateTime updated_at = json["updated_at"]?.GetValue<DateTime?>() ?? new DateTime();
            bool is_customizable = json["is_customizable"]?.GetValue<bool?>() ?? false;

            List<Stop> orderedStopsList = [];

            return CreateItinerary(id, title, description, status, 
                                    created_by, updated_by, created_at, updated_at, 
                                    is_customizable, orderedStopsList);
        }


        /// <summary>
        /// Primary method to create an Itinerary Object from the provided parameters.
        /// </summary>
        /// <returns>The generated Itinerary object.</returns>
        public static Itinerary CreateItinerary(int id, string title, string description, PublicationStatusEnum status, 
                                                    string created_by, string updated_by, DateTime created_at, DateTime updated_at, 
                                                    bool is_customizable, List<Stop> orderedStopsList)
        {
            return new Itinerary(id, title, description, status, 
                                    created_by, updated_by, created_at, updated_at, 
                                    is_customizable, orderedStopsList);
        }
    }
}