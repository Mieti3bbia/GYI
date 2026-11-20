namespace FYI.DataModels.Entities;

public sealed class Itinerary
{
    public const string DraftStatus = "Draft";

    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Theme { get; set; }
    public required string Description { get; set; }
    public string? GeographicArea { get; set; }
    public required string Status { get; set; }
    public bool IsCustomizable { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
