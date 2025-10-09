using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests;

public abstract class TestBase : IDisposable
{
    protected IDomainService DomainService;
    protected ICosmosDataStore DataStore;
    protected FakeTimeProvider TimeProvider;

    private ActivitySource _activitySource;
    private ActivityListener _activityListener;

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
        TimeProvider = new FakeTimeProvider();
        var httpContextAccessor = CreateHttpContextAccessor();

        var inMemoryCosmosStorage = new InMemoryCosmosStorage();
        DataStore = new InMemoryCosmosDataStore(inMemoryCosmosStorage, TimeProvider, httpContextAccessor);
        DomainService = new InMemoryCosmosDomainService(inMemoryCosmosStorage, TimeProvider, httpContextAccessor);
    }

    private static IHttpContextAccessor CreateHttpContextAccessor()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "TestUser")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        context.User = principal;

        httpContextAccessor.HttpContext.Returns(context);
        return httpContextAccessor;
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
