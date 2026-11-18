namespace FYI.DataModels.Entities;

public sealed class Stop
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public decimal Cap { get; set; }
    public decimal Price { get; set; }
    public decimal CoordinateX { get; set; }
    public decimal CoordinateY { get; set; }
    public Guid ProposedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
