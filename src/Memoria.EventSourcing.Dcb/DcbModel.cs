using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Abstract base class for models whose consistency boundary is a <see cref="TagQuery"/>. It adds
/// the global position a fold reached to the <see cref="EventSourcedModel"/> behaviour shared by
/// every model, and is the base both <see cref="DcbAggregateRoot"/> and <see cref="DcbProjection"/>
/// derive from.
/// </summary>
/// <remarks>
/// The mirror of <see cref="StreamedModel"/>: each consistency model adds its own notion of "how far
/// this was folded" to the shared fold, and neither inherits the other's.
/// </remarks>
public abstract class DcbModel : EventSourcedModel, IDcbModel
{
    /// <summary>
    /// Gets or sets the global position of the latest event folded into this model.
    /// </summary>
    [JsonIgnore]
    public long LatestPosition { get; set; }

    /// <summary>
    /// Gets or sets the tags of the boundary this model was folded from.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<Tag> Tags { get; set; } = [];
}
