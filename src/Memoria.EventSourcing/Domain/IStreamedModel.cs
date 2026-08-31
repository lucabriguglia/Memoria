namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the members shared by every event-sourced model whose consistency boundary is a
/// <em>stream</em>: the model's events all belong to one stream, and optimistic concurrency is
/// expressed against that stream's latest sequence.
/// </summary>
/// <remarks>
/// This sits between <see cref="IEventSourcedModel"/> — which owns only what is true of any model
/// rebuilt by applying events — and the write and read models themselves. Consistency models that
/// do not group events into streams derive from <see cref="IEventSourcedModel"/> directly and
/// never acquire a <see cref="StreamId"/>.
/// </remarks>
public interface IStreamedModel : IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the unique identifier for the event stream associated with this model.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies the event stream containing this model's domain events.
    /// This is typically derived from the model's identifier and type information.
    /// </value>
    string StreamId { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the latest event applied to this model.
    /// </summary>
    /// <value>
    /// An integer representing the sequence position of the most recent event in the event stream.
    /// Used for event ordering and ensuring proper event application sequence.
    /// </value>
    /// <remarks>
    /// A sequence is per-stream, so it lives here rather than on <see cref="IEventSourcedModel"/>.
    /// A consistency model with one global ordering needs a wider counter, not this one.
    /// </remarks>
    int LatestEventSequence { get; set; }
}
