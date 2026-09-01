using System.Reflection;
using Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;
using Memoria.EventSourcing.Dcb.Tests.Models.Projections;
using Memoria.EventSourcing.Domain;
using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

/// <summary>
/// The mirror of <c>StreamedModelTests</c>: each consistency model adds its own notion of how far a
/// fold reached, and neither inherits the other's.
/// </summary>
public class DcbModelTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void Dcb_models_record_how_far_they_folded_as_a_global_position()
    {
        typeof(IDcbModel).GetProperty("LatestPosition", AnyMember).Should().NotBeNull();
        typeof(IDcbModel).Should().BeAssignableTo<IEventSourcedModel>();
    }

    [Fact]
    public void A_position_is_wide_enough_for_a_whole_log()
    {
        // A stream sequence counts within one stream and fits an int. A DCB position counts every
        // event in the store, so an int would cap the store itself at about 2.1 billion events.
        typeof(IDcbModel).GetProperty("LatestPosition")!.PropertyType.Should().Be<long>();
    }

    [Fact]
    public void Dcb_models_carry_no_stream_sequence()
    {
        typeof(SeatAggregate).GetProperty("LatestEventSequence", AnyMember).Should().BeNull();
        typeof(SeatProjection).GetProperty("LatestEventSequence", AnyMember).Should().BeNull();
    }

    [Fact]
    public void Both_dcb_models_derive_from_the_shared_dcb_base()
    {
        new SeatAggregate().Should().BeAssignableTo<DcbModel>();
        new SeatProjection().Should().BeAssignableTo<DcbModel>();
    }

    [Fact]
    public void Both_dcb_models_carry_the_boundary_they_were_folded_from()
    {
        // Tags are on the shared base, not on the write model alone. A read model differs from a
        // write model only in never producing events; what it was built from is not a write concern.
        typeof(IDcbModel).GetProperty("Tags", AnyMember).Should().NotBeNull();

        new SeatAggregate().Should().BeAssignableTo<IDcbModel>();
        new SeatProjection().Should().BeAssignableTo<IDcbModel>();

        var projection = new SeatProjection { Tags = [new Tag("seat", "a1")] };
        ((IDcbModel)projection).Tags.Should().ContainSingle();
    }

    [Fact]
    public void The_position_round_trips_through_the_dcb_model()
    {
        var aggregate = new SeatAggregate { LatestPosition = 9_000_000_000L };
        var projection = new SeatProjection { LatestPosition = 9_000_000_000L };

        ((IDcbModel)aggregate).LatestPosition.Should().Be(9_000_000_000L);
        ((IDcbModel)projection).LatestPosition.Should().Be(9_000_000_000L, "well past what an int holds");
    }
}
