namespace FYI.DataModels.Entities;

public sealed class ItineraryStop
{
    public Guid Id { get; set; }
    public Guid ItineraryId { get; set; }
    public Guid StopId { get; set; }
    public decimal Order { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
