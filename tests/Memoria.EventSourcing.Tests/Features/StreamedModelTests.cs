using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Aggregates;
using Memoria.EventSourcing.Tests.Models.Projections;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

/// <summary>
/// Stream identity is deliberately <em>not</em> part of <see cref="EventSourcedModel"/>. Consistency
/// models that do not group events into streams reuse the fold — version tracking, the event type
/// filter, <c>Apply</c> — by deriving from <see cref="EventSourcedModel"/> directly, and would
/// silently inherit a meaningless <c>StreamId</c> if it were ever moved back up.
/// </summary>
public class StreamedModelTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void The_shared_event_sourced_model_owns_nothing_stream_specific()
    {
        // Both of these describe a position within one stream. A consistency model with a single
        // global ordering needs neither, and would need a wider counter than an int for the second.
        typeof(EventSourcedModel).GetProperty("StreamId", AnyMember | BindingFlags.DeclaredOnly)
            .Should().BeNull("stream identity belongs to StreamedModel, not to every event-sourced model");
        typeof(EventSourcedModel).GetProperty("LatestEventSequence", AnyMember | BindingFlags.DeclaredOnly)
            .Should().BeNull("a sequence is a position within a stream");

        typeof(IEventSourcedModel).GetProperty("StreamId", AnyMember)
            .Should().BeNull("a model rebuilt by applying events need not belong to a stream");
        typeof(IEventSourcedModel).GetProperty("LatestEventSequence", AnyMember)
            .Should().BeNull();
    }

    [Fact]
    public void The_shared_event_sourced_model_still_owns_the_fold()
    {
        typeof(IEventSourcedModel).GetProperty("Version", AnyMember).Should().NotBeNull();
        typeof(IEventSourcedModel).GetProperty("EventTypeFilter", AnyMember).Should().NotBeNull();
        typeof(IEventSourcedModel).GetMethod("Apply", AnyMember).Should().NotBeNull();
        typeof(IEventSourcedModel).GetMethod("IsEventHandled", AnyMember).Should().NotBeNull();
    }

    [Fact]
    public void Streamed_models_carry_stream_identity_and_a_stream_sequence()
    {
        typeof(IStreamedModel).GetProperty("StreamId", AnyMember).Should().NotBeNull();
        typeof(IStreamedModel).GetProperty("LatestEventSequence", AnyMember).Should().NotBeNull();
        typeof(IStreamedModel).Should().BeAssignableTo<IEventSourcedModel>();
    }

    [Fact]
    public void Write_and_read_models_are_both_streamed_models()
    {
        var aggregate = new ItemAggregate();
        var projection = new ItemProjection();

        aggregate.Should().BeAssignableTo<StreamedModel>();
        aggregate.Should().BeAssignableTo<IStreamedModel>();

        projection.Should().BeAssignableTo<StreamedModel>();
        projection.Should().BeAssignableTo<IStreamedModel>();
    }

    [Fact]
    public void Stream_identity_round_trips_through_the_streamed_model()
    {
        var aggregate = new ItemAggregate { StreamId = "item-1", LatestEventSequence = 7 };
        var projection = new ItemProjection { StreamId = "item-1", LatestEventSequence = 7 };

        aggregate.StreamId.Should().Be("item-1");
        projection.StreamId.Should().Be("item-1");

        ((IStreamedModel)aggregate).StreamId.Should().Be("item-1");
        ((IStreamedModel)aggregate).LatestEventSequence.Should().Be(7);
        ((IStreamedModel)projection).StreamId.Should().Be("item-1");
        ((IStreamedModel)projection).LatestEventSequence.Should().Be(7);
    }
}
