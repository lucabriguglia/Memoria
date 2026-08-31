using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class for event-sourced models whose consistency boundary is a <em>stream</em>.
/// It adds stream identity to the <see cref="EventSourcedModel"/> behaviour shared by every model,
/// and is the base both <see cref="AggregateRoot"/> (write model) and <see cref="Projection"/>
/// (read model) derive from.
/// </summary>
/// <remarks>
/// Stream identity lives here rather than on <see cref="EventSourcedModel"/> so that consistency
/// models which do not group events into streams can reuse the fold — version tracking,
/// <see cref="EventSourcedModel.EventTypeFilter"/> and
/// <see cref="EventSourcedModel.IsEventHandled"/> — without inheriting an identifier that means
/// nothing to them.
/// </remarks>
public abstract class StreamedModel : EventSourcedModel, IStreamedModel
{
    /// <summary>
    /// Gets or sets the stream ID.
    /// </summary>
    [JsonIgnore]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the latest event sequence.
    /// </summary>
    [JsonIgnore]
    public int LatestEventSequence { get; set; }
}
