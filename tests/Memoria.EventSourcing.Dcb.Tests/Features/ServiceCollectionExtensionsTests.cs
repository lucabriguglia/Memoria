using FluentAssertions;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;
using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Dcb.Tests.Models.Projections;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

/// <summary>
/// DCB registration, and its interaction with the streamed model's registration.
/// </summary>
/// <remarks>
/// Events are shared: an <see cref="IEvent"/> is the same event whichever consistency model appends
/// it, and two different CLR types claiming one binding key is a real bug worth surfacing.
/// Aggregates and projections are not: a "Seat" aggregate may legitimately exist in both models, so
/// each keeps its own map.
/// </remarks>
public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly Dictionary<string, Type> _originalEvents = TypeBindings.EventTypeBindings;
    private readonly Dictionary<string, Type> _originalAggregates = TypeBindings.AggregateTypeBindings;
    private readonly Dictionary<string, Type> _originalProjections = TypeBindings.ProjectionTypeBindings;
    private readonly Dictionary<string, Type> _originalDcbAggregates = DcbTypeBindings.AggregateTypeBindings;
    private readonly Dictionary<string, Type> _originalDcbProjections = DcbTypeBindings.ProjectionTypeBindings;

    public void Dispose()
    {
        TypeBindings.EventTypeBindings = _originalEvents;
        TypeBindings.AggregateTypeBindings = _originalAggregates;
        TypeBindings.ProjectionTypeBindings = _originalProjections;
        DcbTypeBindings.AggregateTypeBindings = _originalDcbAggregates;
        DcbTypeBindings.ProjectionTypeBindings = _originalDcbProjections;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Registration_binds_dcb_aggregates_and_projections_to_their_own_maps()
    {
        new ServiceCollection().AddMemoriaDcb(typeof(SeatAggregate));

        DcbTypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(SeatAggregate));
        DcbTypeBindings.ProjectionTypeBindings.Should().Contain("SeatSummary:1", typeof(SeatProjection));
    }

    [Fact]
    public void Registration_binds_events_to_the_map_both_models_share()
    {
        new ServiceCollection().AddMemoriaDcb(typeof(SeatAggregate));

        TypeBindings.EventTypeBindings.Should().Contain("SeatReserved:1", typeof(SeatReservedEvent));
    }

    [Fact]
    public void Registration_does_not_bind_dcb_models_into_the_streamed_maps()
    {
        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>();

        new ServiceCollection().AddMemoriaDcb(typeof(SeatAggregate));

        TypeBindings.AggregateTypeBindings.Should().NotContainValue(typeof(SeatAggregate));
    }

    [Fact]
    public void Registration_registers_the_default_dcb_domain_service()
    {
        var services = new ServiceCollection();

        services.AddMemoriaDcb(typeof(SeatAggregate));

        var descriptor = services.Should()
            .ContainSingle(service => service.ServiceType == typeof(IDcbDomainService)).Subject;
        descriptor.ImplementationType.Should().Be<DefaultDcbDomainService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Both_models_can_be_registered_together_in_either_order()
    {
        var services = new ServiceCollection();

        var act = () =>
        {
            services.AddMemoriaDcb(typeof(SeatAggregate));
            services.AddMemoriaEventSourcing(typeof(StreamedSeatAggregate));
        };

        act.Should().NotThrow();

        // The streamed registration must not have discarded what DCB bound, and vice versa.
        TypeBindings.EventTypeBindings.Should().Contain("SeatReserved:1", typeof(SeatReservedEvent));
        DcbTypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(SeatAggregate));
        TypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(StreamedSeatAggregate));
    }

    [Fact]
    public void Registering_the_streamed_model_first_also_keeps_both()
    {
        var services = new ServiceCollection();

        services.AddMemoriaEventSourcing(typeof(StreamedSeatAggregate));
        services.AddMemoriaDcb(typeof(SeatAggregate));

        TypeBindings.EventTypeBindings.Should().Contain("SeatReserved:1", typeof(SeatReservedEvent));
        DcbTypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(SeatAggregate));
        TypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(StreamedSeatAggregate));
    }

    [Fact]
    public void An_aggregate_name_may_be_reused_across_the_two_models()
    {
        // Both are [AggregateType("Seat")]. Sharing one map would make this a collision and force
        // an application adopting DCB to rename its existing aggregates.
        new ServiceCollection().AddMemoriaDcb(typeof(SeatAggregate));
        new ServiceCollection().AddMemoriaEventSourcing(typeof(StreamedSeatAggregate));

        DcbTypeBindings.AggregateTypeBindings["Seat:1"].Should().Be<SeatAggregate>();
        TypeBindings.AggregateTypeBindings["Seat:1"].Should().Be<StreamedSeatAggregate>();
    }

    [Fact]
    public void An_event_binding_key_claimed_by_two_different_types_still_fails_loudly()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { ["SeatReserved:1"] = typeof(SeatReleasedEvent) };

        var act = () => new ServiceCollection().AddMemoriaDcb(typeof(SeatAggregate));

        act.Should().Throw<InvalidOperationException>().WithMessage("*SeatReserved:1*");
    }

    [Fact]
    public void Registering_the_same_assembly_twice_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddMemoriaDcb(typeof(SeatAggregate));
        var act = () => services.AddMemoriaDcb(typeof(SeatAggregate));

        act.Should().NotThrow();
        DcbTypeBindings.AggregateTypeBindings.Should().Contain("Seat:1", typeof(SeatAggregate));
    }
}
