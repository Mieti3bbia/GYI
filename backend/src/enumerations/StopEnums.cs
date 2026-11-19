namespace GYI.src.enumerations
{
    /// <summary>
    /// Enumeration to indicate the type of stop:
    ///     - Fixed: The stop has been set by the editor who has published the itinerary; can't be removed by the tourist.
    ///     - Facultative: The stop is optional and can be added or removed by the tourist.
    /// </summary>
    public enum StopTypeEnum
    {
        Fixed,
        Facultative
    }
}