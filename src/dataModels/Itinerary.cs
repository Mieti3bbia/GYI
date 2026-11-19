using GYI.src.enumerations;

using GYI.src.dataModels.interfaces;

using System.Text.Json.Nodes;


namespace GYI.src.dataModels
{
    /// <summary>
    /// Class representing an itinerary, which is a collection of stops and other metadata;
    /// supposed to be generated from the database table "itineraries" and the intermediate table "itinerary_stops".
    ///     The stops order is represented by the orderedStopsList field, which is an ordered list of Stop objects.
    /// </summary>
    public class Itinerary(int id, string title, string description, PublicationStatusEnum status,
                            string created_by, string updated_by, DateTime created_at, DateTime updated_at, 
                            bool is_customizable, List<Stop> orderedStopsList) : IDataModel
    {
        // fields belonging to the DB schema
        public int Id
        { get; set; } = id;
        public string Title
        { get; set; } = title;
        public string Description
        { get; set; } = description;
        public PublicationStatusEnum Status
        { get; set; } = status;
        public string Created_by  // fk to user
        { get; set; } = created_by;
        public string Updated_by  // fk to user
        { get; set; } = updated_by;
        public DateTime Created_at
        { get; set; } = created_at;
        public DateTime Updated_at
        { get; set; } = updated_at;
        public bool Is_customizable
        { get; set; } = is_customizable;

        // fields not belonging to the DB schema
        /// <summary>
        /// The stops in this list are supposed to be the same as the ones stored in the DB table "stops",
        ///     while their order is an information related to the intermediate table "itinerary_stops"
        ///     (which is not represented in the code as a class, but rather as an ordered list)
        /// </summary>
        public List<Stop> OrderedStopsList
        { get; set; } = orderedStopsList;


        /// <summary>
        /// Adds a stop to the orderedStopsList of this itinerary at the specified index.
        ///     The index is 0-based, and if it is negative, the stop will be added at the end of the list.
        /// The method ensures that the same stop is not added in two consecutive positions, which would be an invalid itinerary.
        /// </summary>
        /// <returns>The operation outcome.</returns>
        public bool AddStop(Stop stop, int index = -1)
        {
            if(index < 0)
            {
                index = OrderedStopsList.Count; // negative indexing is not always supported
            }

            // Ensuring to not have the same stop in two consecutive positions, which would be an invalid itinerary.
            if(OrderedStopsList[index] == stop || // check the previous element
                index != -1 && index-1 < OrderedStopsList.Count // check it's not the last element
                && OrderedStopsList[index+1] == stop) // check the next element (if the new stop is not being added at the end of the list)
            {
                // TODO: consider adding a log
                return false; // insertion refused
            }

            OrderedStopsList.Insert(index, stop);
            return OrderedStopsList[index] == stop;
        }

        /// <summary>
        /// Removes a stop from the orderedStopsList of this itinerary.
        ///     The method ensures that fixed stops can't be removed, 
        ///     as they are set by the editor who has published the itinerary.
        /// </summary>
        public bool RemoveStop(int index)
        {
            if(index < 0 || index >= OrderedStopsList.Count)
            {
                return false; // invalid index
            }

            Stop stop = OrderedStopsList[index];
            if(stop.Type == StopTypeEnum.Fixed)
            {
                return false; // fixed stops can't be removed
            }

            OrderedStopsList.RemoveAt(index);
            return stop != OrderedStopsList[index]; // check if the stop was actually removed
        }

        /// <summary>
        /// Returns a list of segments that connect the stops in the orderedStopsList of this itinerary.
        ///     Each segment connects two consecutive stops in the orderedStopsList.
        /// The returned list contains on-demand generated Segment objects, which are not stored in the database,
        ///     however segments can provide the information related to the database table "itinerary_segments"
        ///     for eventual future implementations that may require it.
        /// </summary>
        /// <returns></returns>
        public List<Segment> GetSegments()
        {
            if(OrderedStopsList.Count < 2)
            {
                return [];
            }

            List<Segment> listOfSegments = [];
            for(short i = 0; i < OrderedStopsList.Count - 1; i++)
            {
                var departureStop = OrderedStopsList[i];
                var arrivalStop = OrderedStopsList[i + 1];
                var segment = new Segment(Id, departureStop, arrivalStop, i);
                listOfSegments.Add(segment);
            }

            return listOfSegments;
        }

        
        // interface method
        public JsonObject GetJSONData()
        {
            return new JsonObject
            {
                ["id"] = Id,
                ["title"] = Title,
                ["description"] = Description,
                ["status"] = Status.ToString(),
                ["created_by"] = Created_by,
                ["updated_by"] = Updated_by,
                ["created_at"] = Created_at.ToString("o"),  // ISO 8601 format
                ["updated_at"] = Updated_at.ToString("o"),  // ISO 8601 format
                ["is_customizable"] = Is_customizable
            };
        }
    }
}