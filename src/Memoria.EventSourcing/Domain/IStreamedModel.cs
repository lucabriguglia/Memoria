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
}
