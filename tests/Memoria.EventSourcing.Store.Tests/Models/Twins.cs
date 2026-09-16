using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models;

// The twins: types carrying the same attribute name and version as the shared test models — so
// they write and read the same stored keys — on different CLR types, so which set resolved a key
// is visible in what a read returns. Bound only by a TypeBindingSet a test builds, never by the
// process-wide one, which is what lets a store test prove it read through the set it was given.

[EventType("TestAggregateCreated")]
public record TwinCreatedEvent(string Id, string Name, string Description) : IEvent;

[EventType("TestAggregateUpdated")]
public record TwinUpdatedEvent(string Id, string Name, string Description) : IEvent;

[AggregateType("TestAggregate1")]
public class TwinAggregate : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(TwinCreatedEvent), typeof(TwinUpdatedEvent)];

    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public TwinAggregate()
    {
    }

    public TwinAggregate(string id, string name, string description)
    {
        Add(new TwinCreatedEvent(id, name, description));
    }

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case TwinCreatedEvent created:
                Id = created.Id;
                Name = created.Name;
                Description = created.Description;
                return true;
            case TwinUpdatedEvent updated:
                Name = updated.Name;
                Description = updated.Description;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>The same store id the shared aggregate writes under, so the twin reads its snapshot.</summary>
public class TwinAggregateId(string id) : IAggregateId<TwinAggregate>
{
    public string Id => $"test-aggregate-1:{id}";
    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>();
}

[ProjectionType("TestProjection")]
public class TwinProjection : Projection
{
    public override Type[] EventTypeFilter { get; } = [typeof(TwinCreatedEvent), typeof(TwinUpdatedEvent)];

    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case TwinCreatedEvent created:
                Name = created.Name;
                Description = created.Description;
                return true;
            case TwinUpdatedEvent updated:
                Name = updated.Name;
                Description = updated.Description;
                return true;
            default:
                return false;
        }
    }
}

public class TwinProjectionId(string id) : IProjectionId<TwinProjection>
{
    public string Id => id;
    public IDictionary<string, string>? EventPropertyFilter => null;
}

/// <summary>
/// The shared test models' keys bound to the twins: what a store under test is given so that a row
/// the shared model wrote comes back as the twin.
/// </summary>
public static class TwinBindings
{
    public static TypeBindingSet Create() => new()
    {
        EventTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregateCreated:1", typeof(TwinCreatedEvent) },
            { "TestAggregateUpdated:1", typeof(TwinUpdatedEvent) }
        },
        AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregate1:1", typeof(TwinAggregate) }
        },
        ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "TestProjection:1", typeof(TwinProjection) }
        }
    };
}
