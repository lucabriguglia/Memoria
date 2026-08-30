using FluentAssertions;
using Memoria.EventSourcing.Store.Cosmos.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.Registration;

/// <summary>
/// <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> is designed to live for the lifetime of the
/// application: each instance performs its own account discovery and opens its own connections, and
/// disposing one throws all of that away. These tests pin that <c>AddMemoriaCosmos</c> creates one
/// client for the application, that ending a scope does not tear it down, and that it is released
/// when the application is.
/// </summary>
/// <remarks>
/// No emulator is needed. Constructing a <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> performs
/// no I/O — the account is contacted on first use — and these tests never issue a request.
/// </remarks>
public class CosmosClientLifetimeTests
{
    private const string Endpoint = "https://localhost:8081";

    private const string AuthKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMemoriaCosmos(options =>
        {
            options.Endpoint = Endpoint;
            options.AuthKey = AuthKey;
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public void GivenTwoScopes_ThenBothUseTheSameCosmosClient()
    {
        using var serviceProvider = BuildServiceProvider();
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        var firstClient = firstScope.ServiceProvider.GetRequiredService<CosmosClientProvider>().Client;
        var secondClient = secondScope.ServiceProvider.GetRequiredService<CosmosClientProvider>().Client;

        firstClient.Should().BeSameAs(secondClient);
    }

    [Fact]
    public void GivenAScopeThatResolvedTheStoreIsDisposed_ThenTheSharedCosmosClientStillWorks()
    {
        using var serviceProvider = BuildServiceProvider();
        var shared = serviceProvider.GetRequiredService<CosmosClientProvider>();

        using (var scope = serviceProvider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICosmosDataStore>();
            scope.ServiceProvider.GetRequiredService<IDomainService>();
        }

        // GetContainer is the cheapest call that throws ObjectDisposedException on a disposed
        // client, so it detects a scoped service having disposed the shared one.
        var useSharedClient = () => shared.Client.GetContainer("Memoria", "Domain");

        useSharedClient.Should().NotThrow();
    }

    [Fact]
    public void GivenTheApplicationIsShutDown_ThenTheSharedCosmosClientIsDisposed()
    {
        CosmosClientProvider shared;

        using (var serviceProvider = BuildServiceProvider())
        {
            shared = serviceProvider.GetRequiredService<CosmosClientProvider>();
        }

        var useSharedClient = () => shared.Client.GetContainer("Memoria", "Domain");

        useSharedClient.Should().Throw<ObjectDisposedException>();
    }
}
