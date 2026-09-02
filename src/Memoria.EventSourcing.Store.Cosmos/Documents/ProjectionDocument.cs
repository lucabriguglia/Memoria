using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.Cosmos.Documents;

/// <summary>
/// Represents a document that stores a projection (read model) snapshot for event sourcing in a Cosmos DB
/// implementation. The document contains metadata and the serialized state of the projection.
/// Projection documents share the same container as aggregate documents and are distinguished by their
/// <c>documentType</c>.
/// </summary>
public class ProjectionDocument
{
    /// <summary>
    /// Gets or sets the unique identifier of the stream the projection is derived from.
    /// </summary>
    [JsonProperty("streamId")]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets the document type identifier, used to distinguish projection snapshots from other documents
    /// stored in the same container.
    /// </summary>
    [JsonProperty("documentType")]
    /// <remarks>
    /// An instance property, not a static one, so the value round-trips. All three document types
    /// share one container and one partition key, and their ids are built from different things, so
    /// a point read by id can return a document of the wrong kind. Reading this back is what makes
    /// that detectable rather than a null field somewhere further down.
    /// </remarks>
    public string DocumentType { get; set; } = Documents.DocumentType.Projection;

    /// <summary>
    /// Gets or sets the type of the projection, typically represented in a "Name:Version" format.
    /// </summary>
    [JsonProperty("projectionType")]
    public string ProjectionType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the projection snapshot.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version of the projection, representing the number of events applied.
    /// </summary>
    [JsonProperty("version")]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the most recent event applied to the projection.
    /// </summary>
    [JsonProperty("latestEventSequence")]
    public int LatestEventSequence { get; set; }

    /// <summary>
    /// Gets or sets the serialized representation of the projection's state.
    /// </summary>
    [JsonProperty("data")]
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time when the projection snapshot was initially created.
    /// </summary>
    [JsonProperty("createdDate")]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user or process that originally created the projection snapshot.
    /// </summary>
    [JsonProperty("createdBy")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent update made to the projection snapshot.
    /// </summary>
    [JsonProperty("updatedDate")]
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last modified the projection snapshot.
    /// </summary>
    [JsonProperty("updatedBy")]
    public string? UpdatedBy { get; set; }
}
