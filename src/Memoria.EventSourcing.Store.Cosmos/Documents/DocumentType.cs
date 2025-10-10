namespace Memoria.EventSourcing.Store.Cosmos.Documents;

/// <summary>
/// Defines the available document types used in the Cosmos DB Event Sourcing store.
/// These constants are used to categorize and identify different types of documents stored in the database.
/// </summary>
public static class DocumentType
{
    /// <summary>
    /// Gets the document type identifier for event documents.
    /// This constant is used to mark documents that represent individual domain events.
    /// </summary>
    public static string Event => "Event";

    /// <summary>
    /// Gets the document type identifier for aggregate documents.
    /// This constant is used to mark documents that represent aggregate root information.
    /// </summary>
    public static string Aggregate => "Aggregate";

    /// <summary>
    /// Gets the document type identifier for aggregate event documents.
    /// This constant is used to mark documents that represent events associated with specific aggregates.
    /// </summary>
    public static string AggregateEvent => "AggregateEvent";
}
