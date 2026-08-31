using System.Security.Claims;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// A DCB context over SQLite, with real SQL, real transactions and real concurrency tokens.
/// </summary>
/// <remarks>
/// The append path cannot be exercised on the in-memory provider: it models neither the transaction
/// the append opens nor the concurrency token the tag head rows carry, so an append against it would
/// report a success it has not actually guaranteed. SQLite gives both, in-process and with no
/// container. The same behaviour is verified against SQL Server and PostgreSQL in the container
/// tests, which is where the provider-specific parts of the mapping are proven.
/// </remarks>
public abstract class RelationalTestBase : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly Dictionary<string, Type> _originalEventTypeBindings = TypeBindings.EventTypeBindings;

    protected readonly TestDbContext Context;
    protected readonly FakeTimeProvider TimeProvider;

    protected RelationalTestBase()
    {
        // Shared-cache in-memory SQLite: every connection opened against this name sees the same
        // database, which is what lets a test open a second context and race the first.
        _connection = new SqliteConnection($"DataSource=dcb-{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Context = CreateContext();

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };

        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(SeatAggregate) }
        };

        DcbTypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "SeatSummary:1", typeof(SeatSummaryProjection) }
        };
    }

    /// <summary>
    /// Opens an independent context over the same database, for interleaving two appends.
    /// </summary>
    /// <param name="interceptors">Optional interceptors, used to reproduce a mid-append race.</param>
    protected TestDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptors)
                .Options,
            TimeProvider,
            CreateHttpContextAccessor());

    public async Task InitializeAsync() => await Context.Database.EnsureCreatedAsync();

    public Task DisposeAsync()
    {
        TypeBindings.EventTypeBindings = _originalEventTypeBindings;
        Context.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private static IHttpContextAccessor CreateHttpContextAccessor()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "TestUser")], "TestAuth"))
        };

        httpContextAccessor.HttpContext.Returns(context);
        return httpContextAccessor;
    }
}

public class TestDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor);
