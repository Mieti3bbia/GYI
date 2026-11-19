namespace GYI.src.enumerations
{
    /// <summary>
    /// Enumeration to indicate the publication status of a data model:
    ///    - Unknown: The publication status couldn't be read from the database; probably missing or invalid data.
    ///    - Proposed: The data model has been proposed by an operator and is waiting for approval by the editor.
    ///    - Accepted: The data model has been approved by the editor and is now available.
    /// </summary>
    public enum PublicationStatusEnum
    {
        Unknown,
        Proposed,
        Accepted
    }
}