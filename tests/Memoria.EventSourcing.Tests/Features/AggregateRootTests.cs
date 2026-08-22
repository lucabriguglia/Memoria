using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Aggregates;
using Memoria.EventSourcing.Tests.Models.Events;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

public class AggregateRootTests
{
    [Fact]
    public void Adding_an_event_stages_it_as_uncommitted_applies_state_and_increments_version()
    {
        var aggregate = new ItemAggregate("item-1", "First");

        aggregate.Id.Should().Be("item-1");
        aggregate.Name.Should().Be("First");
        aggregate.Version.Should().Be(1);
        aggregate.UncommittedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ItemCreatedEvent>();
    }

    [Fact]
    public void Further_commands_stage_additional_uncommitted_events()
    {
        var aggregate = new ItemAggregate("item-1", "First");

        aggregate.Rename("Renamed");

        aggregate.Name.Should().Be("Renamed");
        aggregate.Version.Should().Be(2);
        aggregate.UncommittedEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Applying_events_rebuilds_state_without_staging_uncommitted_events()
    {
        var aggregate = new ItemAggregate();

        aggregate.Apply(new IEvent[]
        {
            new ItemCreatedEvent("item-1", "First"),
            new ItemRenamedEvent("item-1", "Renamed")
        });

        aggregate.Name.Should().Be("Renamed");
        aggregate.Version.Should().Be(2);
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Aggregate_is_both_an_event_sourced_model_and_an_aggregate_root()
    {
        var aggregate = new ItemAggregate();

        aggregate.Should().BeAssignableTo<EventSourcedModel>();
        aggregate.Should().BeAssignableTo<IAggregateRoot>();
        aggregate.Should().BeAssignableTo<IEventSourcedModel>();
    }
}
