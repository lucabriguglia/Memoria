using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Events;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

/// <summary>
/// The cached lookups on <see cref="TypeBindings"/>.
/// </summary>
/// <remarks>
/// Both store providers resolve binding keys through these, and the results are cached — the
/// attribute lookups for the lifetime of the process, the inverted event map until
/// <see cref="TypeBindings.EventTypeBindings"/> is assigned a different dictionary. The store suites
/// cannot catch a stale inverted map because every one of them rebinds the same content, so
/// re-registration is only actually covered here.
/// </remarks>
public class TypeBindingsTests : IDisposable
{
    private readonly Dictionary<string, Type> _originalEventTypeBindings = TypeBindings.EventTypeBindings;

    public void Dispose() => TypeBindings.EventTypeBindings = _originalEventTypeBindings;

    [Fact]
    public void GivenAnEventType_WhenTheBindingKeyIsRequested_ThenItComesFromTheAttribute()
    {
        TypeBindings.GetEventBindingKey(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");
    }

    [Fact]
    public void GivenTheBindingKeyWasAlreadyResolved_WhenRequestedAgain_ThenTheSameKeyIsReturned()
    {
        var first = TypeBindings.GetEventBindingKey(typeof(ItemRenamedEvent));
        var second = TypeBindings.GetEventBindingKey(typeof(ItemRenamedEvent));

        second.Should().Be(first);
    }

    [Fact]
    public void GivenATypeWithNoAttribute_WhenTheBindingKeyIsRequested_ThenItThrowsEveryTime()
    {
        var resolve = () => TypeBindings.GetEventBindingKey(typeof(UnattributedEvent));

        using (new AssertionScope())
        {
            // Twice: a throwing factory must cache nothing, so the second call has to throw the same
            // way rather than surfacing a TypeInitializationException or succeeding from a cache.
            resolve.Should().Throw<InvalidOperationException>()
                .WithMessage("*UnattributedEvent*EventType attribute*");
            resolve.Should().Throw<InvalidOperationException>()
                .WithMessage("*UnattributedEvent*EventType attribute*");
        }
    }

    [Fact]
    public void GivenEventTypeBindings_WhenInverted_ThenEachClrTypeMapsToItsKey()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "ItemCreated:1", typeof(ItemCreatedEvent) }
        };

        var bindingKeysByType = TypeBindings.GetEventBindingKeysByType();

        bindingKeysByType.GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");
    }

    [Fact]
    public void GivenAnUnregisteredClrType_WhenInverted_ThenItIsAbsent()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "ItemCreated:1", typeof(ItemCreatedEvent) }
        };

        var bindingKeysByType = TypeBindings.GetEventBindingKeysByType();

        // Callers pass the result of GetValueOrDefault straight into a query filter, so an
        // unregistered type has to come back null rather than throw.
        bindingKeysByType.GetValueOrDefault(typeof(ItemRenamedEvent)).Should().BeNull();
    }

    [Fact]
    public void GivenSeveralKeysBindTheSameClrType_WhenInverted_ThenTheFirstKeyWins()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "ItemCreated:1", typeof(ItemCreatedEvent) },
            { "ItemCreated:2", typeof(ItemCreatedEvent) }
        };

        var bindingKeysByType = TypeBindings.GetEventBindingKeysByType();

        bindingKeysByType.GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");
    }

    [Fact]
    public void GivenTheBindingsAreReplaced_WhenInvertedAgain_ThenTheNewBindingsAreUsed()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "ItemCreated:1", typeof(ItemCreatedEvent) }
        };
        TypeBindings.GetEventBindingKeysByType()
            .GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:1");

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "ItemCreated:7", typeof(ItemCreatedEvent) },
            { "ItemRenamed:1", typeof(ItemRenamedEvent) }
        };

        var bindingKeysByType = TypeBindings.GetEventBindingKeysByType();

        using (new AssertionScope())
        {
            bindingKeysByType.GetValueOrDefault(typeof(ItemCreatedEvent)).Should().Be("ItemCreated:7");
            bindingKeysByType.GetValueOrDefault(typeof(ItemRenamedEvent)).Should().Be("ItemRenamed:1");
        }
    }

    private record UnattributedEvent : IEvent;
}
