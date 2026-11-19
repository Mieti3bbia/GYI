using GYI.src.dataModels.interfaces;

using System.Text.Json.Nodes;


namespace GYI.src.dataModels
{
    /// <summary>
    /// Class representing a segment, which is a part of an itinerary that connects two stops.
    ///     It is an on-demand generated information, which means that it is not stored in the database,
    ///     but rather generated from the orderedStopsList of an itinerary.
    ///         However, the class can provide the information related to the database table "itinerary_segments", 
    ///         which is an intermediate table that connects itineraries and stops.
    /// The departureStop of this segment corresponds to the arrivalStop of the previous segment,
    /// and the arrivalStop of this segment corresponds to the departureStop of the next segment.
    /// </summary>
    /// <param name="departureStop"></param>
    /// <param name="arrivalStop"></param>
    /// <param name="order"></param>
    public class Segment(int itineraryId, Stop departureStop, Stop arrivalStop, short order) : IDataModel
    {
        // fields belonging to the DB schema
        public int ItineraryId  // fk to itinerary
        { get; set; } = itineraryId;

        public Stop DepartureStop  // the stop from which the segment departs
        { get; set; } = departureStop;
        public Stop ArrivalStop  // the stop at which the segment arrives
        { get; set; } = arrivalStop;

        public short Order  // the order of the segment in the itinerary (0-based index)
        { get; set; } = order;


        // interface method
        
        public JsonObject GetJSONData()
        {
            return new JsonObject
            {
                ["itinerary_stop_records"] = new JsonArray
                {
                    new JsonObject
                    {
                        // there is no segment ID because the segments are not stored in the database;
                        // we are extracting the information of two distinct 'itinerary_stop' records.
                        ["itinerary_id"] = ItineraryId,
                        ["stop_id"] = DepartureStop.ID,
                        ["order"] = Order
                    },
                    new JsonObject
                    {
                        ["itinerary_id"] = ItineraryId,
                        ["stop_id"] = ArrivalStop.ID,
                        ["order"] = Order + 1
                    }
                }
            };
        }

    }
}