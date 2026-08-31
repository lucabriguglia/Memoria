using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class for projections (read models) in event sourcing. A projection rebuilds a
/// query-optimised view of state from the shared <see cref="EventSourcedModel"/> behaviour by
/// applying domain events, but it never produces new events, so it has no <c>Add</c> method and no
/// uncommitted-events collection.
/// </summary>
public abstract class Projection : StreamedModel, IProjection
{
    /// <summary>
    /// Gets or sets the projection ID.
    /// </summary>
    [JsonIgnore]
    public string ProjectionId { get; set; } = null!;
}
