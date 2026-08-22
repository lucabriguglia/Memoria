using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Events;
using Memoria.EventSourcing.Tests.Models.Projections;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

public class ProjectionTests
{
    [Fact]
    public void Applying_events_rebuilds_projection_state()
    {
        var projection = new ItemProjection();

        projection.Apply(new IEvent[]
        {
            new ItemCreatedEvent("item-1", "First"),
            new ItemRenamedEvent("item-1", "Renamed")
        });

        projection.Id.Should().Be("item-1");
        projection.Name.Should().Be("Renamed");
    }

    [Fact]
    public void Applying_events_increments_version_for_each_handled_event()
    {
        var projection = new ItemProjection();

        projection.Apply(new IEvent[]
        {
            new ItemCreatedEvent("item-1", "First"),
            new ItemRenamedEvent("item-1", "Renamed")
        });

        projection.Version.Should().Be(2);
    }

    [Fact]
    public void IsEventHandled_honours_the_event_type_filter()
    {
        var projection = new ItemProjection();

        projection.IsEventHandled(typeof(ItemCreatedEvent)).Should().BeTrue();
        projection.IsEventHandled(typeof(UnrelatedEvent)).Should().BeFalse();
    }

    [Fact]
    public void Projection_is_an_event_sourced_model_but_not_an_aggregate_root()
    {
        var projection = new ItemProjection();

        projection.Should().BeAssignableTo<EventSourcedModel>();
        projection.Should().BeAssignableTo<IProjection>();
        projection.Should().NotBeAssignableTo<IAggregateRoot>();
    }

    [Fact]
    public void Projection_does_not_expose_write_model_members()
    {
        var type = typeof(ItemProjection);
        const BindingFlags anyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        type.GetMethod("Add", anyMember).Should().BeNull("projections are read models and never stage events");
        type.GetProperty("UncommittedEvents", anyMember).Should().BeNull("projections have no uncommitted events");
    }

    [EventType("Unrelated")]
    private record UnrelatedEvent : IEvent;
}
