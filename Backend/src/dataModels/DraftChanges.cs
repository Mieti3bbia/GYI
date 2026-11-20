namespace GYI.src.dataModels;

public sealed record DraftChanges(
    string Title, string Theme, string Description, string GeographicArea, DraftStopChange[] Stops);

public sealed record DraftStopChange(Guid? StopId, DraftStopDetails? Details);

public sealed record DraftStopDetails(
    string? Name, string? Description, decimal? Latitude, decimal? Longitude,
    string? Cap, decimal? Price, string? Municipality, string? Province, string? SourceUrl);
