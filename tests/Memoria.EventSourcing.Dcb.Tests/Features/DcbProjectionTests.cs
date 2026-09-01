using System.Reflection;
using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Dcb.Tests.Models.Projections;
using Memoria.EventSourcing.Domain;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

public class DcbProjectionTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void Applying_events_rebuilds_projection_state()
    {
        var projection = new SeatProjection();

        projection.Apply(new IEvent[]
        {
            new SeatReservedEvent("a1", "s7"),
            new SeatReservedEvent("a2", "s8")
        });

        projection.Reservations.Should().Be(2);
        projection.Version.Should().Be(2);
    }

    [Fact]
    public void The_event_type_filter_is_honoured()
    {
        var projection = new SeatProjection();

        projection.IsEventHandled(typeof(SeatReservedEvent)).Should().BeTrue();
        projection.IsEventHandled(typeof(UnrelatedEvent)).Should().BeFalse();
    }

    [Fact]
    public void A_dcb_projection_does_not_expose_write_model_members()
    {
        var type = typeof(SeatProjection);

        // Staging is the only difference between a read model and a write model, so it is the only
        // thing missing here. Tags are not a write-model member: they record the boundary the model
        // was folded from, which a read model has as much as a write model does.
        type.GetMethod("Add", AnyMember).Should().BeNull("projections are read models and never stage events");
        type.GetProperty("UncommittedEvents", AnyMember).Should().BeNull("projections have no uncommitted events");
        type.GetProperty("Tags", AnyMember).Should().NotBeNull("a read model still knows what built it");
    }

    [Fact]
    public void A_dcb_projection_is_an_event_sourced_model_but_belongs_to_no_stream()
    {
        var projection = new SeatProjection();

        projection.Should().BeAssignableTo<EventSourcedModel>();
        projection.Should().BeAssignableTo<IDcbProjection>();

        projection.Should().NotBeAssignableTo<IStreamedModel>();
        projection.Should().NotBeAssignableTo<IProjection>();
        projection.Should().NotBeAssignableTo<IDcbAggregateRoot>();

        typeof(SeatProjection).GetProperty("StreamId", AnyMember)
            .Should().BeNull("a DCB projection has no stream to belong to");
    }
}
