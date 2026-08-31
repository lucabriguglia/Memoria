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
}
