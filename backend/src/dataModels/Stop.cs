using GYI.src.enumerations;

using GYI.src.dataModels.interfaces;

using System.Text.Json.Nodes;


namespace GYI.src.dataModels
{
    public class Stop(int id, StopTypeEnum type, PublicationStatusEnum status, short cap, string name, string description, 
                        float price, double coordinate_x, double coordinate_y, 
                        string proposed_by, DateTime creation_date, DateTime approval_date) : IDataModel
    {
        // fields belonging to the DB schema
        public int ID
        { get; set; } = id;
        public StopTypeEnum Type
        { get; set; } = type;
        public PublicationStatusEnum Status
        { get; set; } = status;
        public short CAP
        { get; set; } = cap;
        public string Name
        { get; set; } = name;
        public string Description
        { get; set; } = description;
        public float Price
        { get; set; } = price;
        public double Coordinate_x
        { get; set; } = coordinate_x;
        public double Coordinate_y 
        { get; set; } = coordinate_y;
        public string Proposed_by   // fk users
        { get; set; } = proposed_by;
        public DateTime Creation_date
        { get; set; } = creation_date;
        public DateTime Approval_date 
        { get; set; } = approval_date;

        
        // interface method
        public JsonObject GetJSONData()
        {
            return new JsonObject
            {
                ["id"] = ID,
                ["type"] = Type.ToString(),
                ["status"] = Status.ToString(),
                ["cap"] = CAP,
                ["name"] = Name,
                ["description"] = Description,
                ["price"] = Price,
                ["coordinate_x"] = Coordinate_x,
                ["coordinate_y"] = Coordinate_y,
                ["proposed_by"] = Proposed_by,
                ["creation_date"] = Creation_date.ToString("o"),  // ISO 8601 format
                ["approval_date"] = Approval_date.ToString("o")   // ISO 8601 format
            };
        }
    }
}