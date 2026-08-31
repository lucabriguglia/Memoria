using System.Reflection;
using Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;
using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Domain;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

public class DcbAggregateRootTests
{
    [Fact]
    public void Adding_an_event_stages_it_with_the_tags_it_was_given()
    {
        var aggregate = new SeatAggregate();

        aggregate.Reserve("a1", "s7");

        var staged = aggregate.UncommittedEvents.Should().ContainSingle().Subject;
        staged.Event.Should().BeOfType<SeatReservedEvent>();
        staged.Tags.Should().BeEquivalentTo([new Tag("seat", "a1"), new Tag("student", "s7")]);
    }

    [Fact]
    public void Adding_an_event_without_tags_falls_back_to_the_aggregates_own_tags()
    {
        // The common case: an aggregate whose every event concerns the same things it does.
        var aggregate = new SeatAggregate { Tags = [new Tag("seat", "a1")] };

        aggregate.Release("a1");

        aggregate.UncommittedEvents.Should().ContainSingle()
            .Which.Tags.Should().BeEquivalentTo([new Tag("seat", "a1")]);
    }

    [Fact]
    public void Adding_an_event_applies_it_and_advances_the_version()
    {
        var aggregate = new SeatAggregate();

        aggregate.Reserve("a1", "s7");

        aggregate.SeatId.Should().Be("a1");
        aggregate.ReservedBy.Should().Be("s7");
        aggregate.Version.Should().Be(1);
    }

    [Fact]
    public void Applying_events_rebuilds_state_without_staging_anything()
    {
        var aggregate = new SeatAggregate();

        aggregate.Apply(new IEvent[]
        {
            new SeatReservedEvent("a1", "s7"),
            new SeatReleasedEvent("a1")
        });

        aggregate.SeatId.Should().Be("a1");
        aggregate.ReservedBy.Should().BeNull();
        aggregate.Version.Should().Be(2);
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void The_event_type_filter_is_honoured_exactly_as_it_is_for_streams()
    {
        var aggregate = new SeatAggregate();

        aggregate.IsEventHandled(typeof(SeatReservedEvent)).Should().BeTrue();
        aggregate.IsEventHandled(typeof(UnrelatedEvent)).Should().BeFalse();
    }

    [Fact]
    public void Uncommitted_events_cannot_be_mutated_by_a_caller()
    {
        // ReadOnlyCollection<T> implements ICollection<T> explicitly, so the cast succeeds; what
        // matters is that going through it cannot change what the aggregate will append.
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var mutate = () => ((ICollection<TaggedEvent>)aggregate.UncommittedEvents)
            .Add(new TaggedEvent(new SeatReleasedEvent("a1"), [new Tag("seat", "a1")]));

        mutate.Should().Throw<NotSupportedException>();
        aggregate.UncommittedEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_dcb_aggregate_is_an_event_sourced_model_but_belongs_to_no_stream()
    {
        // The whole point of moving StreamId onto StreamedModel: the fold is reused, the stream
        // identity is not inherited.
        var aggregate = new SeatAggregate();

        aggregate.Should().BeAssignableTo<EventSourcedModel>();
        aggregate.Should().BeAssignableTo<IEventSourcedModel>();
        aggregate.Should().BeAssignableTo<IDcbAggregateRoot>();

        aggregate.Should().NotBeAssignableTo<IStreamedModel>();
        aggregate.Should().NotBeAssignableTo<StreamedModel>();
        aggregate.Should().NotBeAssignableTo<IAggregateRoot>();

        typeof(SeatAggregate).GetProperty("StreamId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().BeNull("a DCB aggregate has no stream to belong to");
    }
}
