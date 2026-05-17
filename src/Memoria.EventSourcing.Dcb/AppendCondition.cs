namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// The DCB concurrency token: "I built this decision from events matching
/// <see cref="Query"/> up to (and including) <see cref="AfterPosition"/>".
/// An append carrying this condition succeeds only if no event matching <see cref="Query"/>
/// has been written with a <c>GlobalPosition</c> greater than <see cref="AfterPosition"/>.
/// </summary>
public readonly record struct AppendCondition(DcbQuery Query, long AfterPosition)
{
    /// <summary>
    /// Use when the decision requires the absence of any matching event.
    /// </summary>
    public static AppendCondition NoneMatching(DcbQuery query) => new(query, 0L);
}
