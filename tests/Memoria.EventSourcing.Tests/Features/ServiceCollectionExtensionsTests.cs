using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Extensions;
using Memoria.EventSourcing.Tests.Models.Aggregates;
using Memoria.EventSourcing.Tests.Models.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

/// <summary>
/// Assembly scanning and type-binding registration.
/// </summary>
/// <remarks>
/// The bindings are process-wide static state, so registration has to be additive: a second call —
/// whether a second <c>AddMemoriaEventSourcing</c> or a DCB registration alongside it — must not
/// discard what the first one bound. Saved and restored per test, following
/// <see cref="TypeBindingsTests"/>.
/// </remarks>
[Collection(TypeBindingsCollection.Name)]
public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly Dictionary<string, Type> _originalEvents = TypeBindings.EventTypeBindings;
    private readonly Dictionary<string, Type> _originalAggregates = TypeBindings.AggregateTypeBindings;
    private readonly Dictionary<string, Type> _originalProjections = TypeBindings.ProjectionTypeBindings;

    public void Dispose()
    {
        TypeBindings.EventTypeBindings = _originalEvents;
        TypeBindings.AggregateTypeBindings = _originalAggregates;
        TypeBindings.ProjectionTypeBindings = _originalProjections;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Registration_binds_the_attributed_types_it_finds()
    {
        new ServiceCollection().AddMemoriaEventSourcing(typeof(ItemAggregate));

        TypeBindings.EventTypeBindings.Should().Contain("ItemCreated:1", typeof(ItemCreatedEvent));
        TypeBindings.AggregateTypeBindings.Should().Contain("Item:1", typeof(ItemAggregate));
    }

    [Fact]
    public void Registration_registers_the_default_domain_service()
    {
        var services = new ServiceCollection();

        services.AddMemoriaEventSourcing(typeof(ItemAggregate));

        var descriptor = services.Should().ContainSingle(service => service.ServiceType == typeof(IDomainService)).Subject;
        descriptor.ImplementationType.Should().Be<DefaultDomainService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registration_keeps_bindings_that_were_already_present()
    {
        // The interop case: a DCB registration binds its own types, then AddMemoriaEventSourcing
        // runs. Replacing the dictionary here would leave the DCB events unresolvable, and nothing
        // would report it — every read would simply fail to deserialise later.
        // "DcbOnly:1" is a key this assembly's scan cannot produce, so its survival is real
        // evidence rather than the scan simply rebinding it.
        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { ["DcbOnly:1"] = typeof(ForeignEvent) };
        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>();
        TypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>();

        new ServiceCollection().AddMemoriaEventSourcing(typeof(ItemAggregate));

        TypeBindings.EventTypeBindings.Should().Contain("DcbOnly:1", typeof(ForeignEvent));
        TypeBindings.EventTypeBindings.Should().Contain("ItemCreated:1", typeof(ItemCreatedEvent));
    }

    [Fact]
    public void Registering_the_same_assembly_twice_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddMemoriaEventSourcing(typeof(ItemAggregate));
        var act = () => services.AddMemoriaEventSourcing(typeof(ItemAggregate));

        act.Should().NotThrow("re-binding a type to itself is agreement, not a conflict");
        TypeBindings.EventTypeBindings.Should().Contain("ItemCreated:1", typeof(ItemCreatedEvent));
    }

    [Fact]
    public void A_binding_key_claimed_by_two_different_types_fails_loudly()
    {
        // Silently keeping one of them would deserialise events into the wrong CLR type.
        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { ["ItemCreated:1"] = typeof(ForeignEvent) };

        var act = () => new ServiceCollection().AddMemoriaEventSourcing(typeof(ItemAggregate));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ItemCreated:1*")
            .WithMessage($"*{nameof(ForeignEvent)}*")
            .WithMessage($"*{nameof(ItemCreatedEvent)}*");
    }

    [EventType("Foreign")]
    private record ForeignEvent : IEvent;
}
