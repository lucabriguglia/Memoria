namespace OpenCqrs.EventSourcing;

/// <summary>
/// Represents the mode of reading aggregates.
/// </summary>
public enum ReadMode
{
    /// <summary>
    /// Uses only the latest snapshot without trying to apply any subsequent events.
    /// </summary>
    SnapshotOnly,

    /// <summary>
    /// Uses the latest snapshot and applies any subsequent events.
    /// </summary>
    SnapshotWithNewEvents,

    /// <summary>
    /// Uses the latest snapshot if available, otherwise creates a new aggregate from events.
    /// </summary>
    SnapshotOrCreate,

    /// <summary>
    /// Uses the latest snapshot with subsequent events or creates a new aggregate if no snapshot exists.
    /// </summary>
    SnapshotWithNewEventsOrCreate
}
