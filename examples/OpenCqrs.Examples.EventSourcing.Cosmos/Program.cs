// See https://aka.ms/new-console-template for more information

using Memoria;
using Memoria.EventSourcing.Extensions;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.Cosmos.Extensions;
using Memoria.Extensions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using OpenCqrs;
using OpenCqrs.Examples.EventSourcing.Cosmos.Commands;
using OpenCqrs.Examples.EventSourcing.Cosmos.Queries;

var serviceProvider = ConfigureServices();

await CreateDatabase(serviceProvider);

var dispatcher = serviceProvider.GetService<IDispatcher>();

var customerId = Guid.NewGuid();
var orderId = Guid.NewGuid();

await dispatcher!.Send(new PlaceOrderCommand(customerId, orderId, Amount: 25m));
Console.WriteLine("Order placed.");

var aggregate = await dispatcher.Get(new GetOrderQuery(customerId, orderId));
Console.WriteLine($"Order retrieved. Amount: {aggregate.Value!.Amount}.");

Console.ReadLine();
return;

IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    services.AddOpenCqrs(typeof(Program));
    services.AddOpenCqrsEventSourcing(typeof(Program));
    services.AddOpenCqrsCosmos(options =>
    {
        options.Endpoint = "https://localhost:8081";
        options.AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        options.DatabaseName = "your-database-name";
        options.ContainerName = "your-container-name";
        options.ClientOptions = new CosmosClientOptions();
    });

    return services.BuildServiceProvider();
}

static async Task CreateDatabase(IServiceProvider serviceProvider)
{
    try
    {
        Console.WriteLine("Creating database...");
        var cosmosSetup = serviceProvider.GetService<CosmosSetup>();
        await cosmosSetup!.CreateDatabaseAndContainerIfNotExist();
        Console.WriteLine("Database created successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to create database: {ex.Message}");
        throw;
    }
}
