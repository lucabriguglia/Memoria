using System.Diagnostics;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests;

public abstract class TestBase : IDisposable
{
    protected IDomainService DomainService = null!;

    private ActivitySource _activitySource = null!;
    private ActivityListener _activityListener = null!;

    protected TestBase()
    {
        SetupTypeBindings();
        SetupDomainService();
        SetupActivity();
    }

    private static void SetupTypeBindings()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            {"TestAggregateCreated:1", typeof(TestAggregateCreatedEvent)},
            {"TestAggregateUpdated:1", typeof(TestAggregateUpdatedEvent)},
            {"SomethingHappened:1", typeof(SomethingHappenedEvent)}
        };

        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            {"TestAggregate1:1", typeof(TestAggregate1)},
            {"TestAggregate2:1", typeof(TestAggregate2)}
        };
    }

    private void SetupDomainService()
    {
        var dbContext = Shared.CreateTestDbContext();
        DomainService = new EntityFrameworkCoreDomainService(dbContext);
    }

    private void SetupActivity()
    {
        _activitySource = new ActivitySource("TestSource");

        _activityListener = new ActivityListener();
        _activityListener.ShouldListenTo = _ => true;
        _activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        _activityListener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData;
        _activityListener.ActivityStarted = _ => { };
        _activityListener.ActivityStopped = _ => { };

        ActivitySource.AddActivityListener(_activityListener);

        _activitySource.StartActivity();
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _activitySource.Dispose();
        GC.SuppressFinalize(this);
    }
}
