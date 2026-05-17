using Memoria.Results;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// A <see cref="Failure"/> returned when a DCB append observed an event matching the
/// caller's query at a position greater than the captured <see cref="AppendCondition.AfterPosition"/>.
/// </summary>
public sealed record ConcurrencyConflict(
    DcbQuery Query,
    long ExpectedAfterPosition,
    long ObservedPosition)
    : Failure(
        ErrorCode: ErrorCode.UnprocessableEntity,
        Title: "Concurrency conflict",
        Description: $"An event matching the query was observed at position {ObservedPosition}, after the expected position {ExpectedAfterPosition}.");
