namespace Memoria.EventSourcing;

/// <summary>
/// Represents the mode of reading event-sourced models (aggregates or projections).
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
    /// Uses the latest snapshot if available, otherwise creates a new model from events.
    /// </summary>
    SnapshotOrCreate,

    /// <summary>
    /// Uses the latest snapshot with subsequent events or creates a new model if no snapshot exists.
    /// </summary>
    SnapshotWithNewEventsOrCreate
}
