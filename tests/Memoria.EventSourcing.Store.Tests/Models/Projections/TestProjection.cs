using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;

namespace Memoria.EventSourcing.Store.Tests.Models.Projections;

/// <summary>
/// A read model built by folding a stream's events, then persisted as a snapshot. It handles the test
/// aggregate events but not <see cref="SomethingHappenedEvent"/>, so it can prove only handled events
/// are applied.
/// </summary>
[ProjectionType("TestProjection")]
public class TestProjection : Projection
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(TestAggregateCreatedEvent),
        typeof(TestAggregateUpdatedEvent)
    ];

    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int EventsApplied { get; set; }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            TestAggregateCreatedEvent created => Apply(created),
            TestAggregateUpdatedEvent updated => Apply(updated),
            _ => false
        };
    }

    private bool Apply(TestAggregateCreatedEvent @event)
    {
        Name = @event.Name;
        Description = @event.Description;
        EventsApplied++;

        return true;
    }

    private bool Apply(TestAggregateUpdatedEvent @event)
    {
        Name = @event.Name;
        Description = @event.Description;
        EventsApplied++;

        return true;
    }
}
