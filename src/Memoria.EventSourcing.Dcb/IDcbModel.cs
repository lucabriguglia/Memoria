using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines the members shared by every model whose consistency boundary is a
/// <see cref="TagQuery"/> — the DCB counterpart of <see cref="IStreamedModel"/>.
/// </summary>
public interface IDcbModel : IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the global position of the latest event folded into this model.
    /// </summary>
    /// <value>
    /// The position the fold reached, or <see cref="AppendCondition.NoEvents"/> when nothing was
    /// folded.
    /// </value>
    /// <remarks>
    /// A <see cref="long"/>, not the <see cref="int"/> a stream sequence uses. A stream sequence
    /// counts within one stream; a DCB position counts every event in the log, so the same width
    /// would put a ceiling on the whole store rather than on one aggregate's history.
    /// </remarks>
    long LatestPosition { get; set; }

    /// <summary>
    /// Gets or sets the tags of the boundary this model was folded from.
    /// </summary>
    /// <value>
    /// Set by the store from <see cref="IDcbAggregateId.Boundary"/> or
    /// <see cref="IDcbProjectionId.Boundary"/> before the fold begins, so <c>Apply</c> can read it.
    /// A model spanning more than one entity uses it to know which ones it is about.
    /// </value>
    /// <remarks>
    /// On a write model it does double duty as the default for events staged without explicit tags.
    /// A read model stages nothing, so for it this is only ever the record of what it was built from.
    /// </remarks>
    IReadOnlyCollection<Tag> Tags { get; set; }
}
