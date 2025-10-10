// See https://aka.ms/new-console-template for more information

using Memoria;
using Memoria.EventSourcing.Extensions;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Commands;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Data;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Queries;
using Memoria.Extensions;
using Memoria.Validation.FluentValidation.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var serviceProvider = ConfigureServices();

await CreateDatabase(serviceProvider);

var dispatcher = serviceProvider.GetService<IDispatcher>();

var customerId = Guid.NewGuid();
var orderId = Guid.NewGuid();

await dispatcher!.Send(new PlaceOrderCommand(customerId, orderId, Amount: 25m));
Console.WriteLine("Order placed.");

var aggregate = await dispatcher.Get(new GetOrderAggregateQuery(customerId, orderId));
Console.WriteLine($"Order retrieved. Amount: {aggregate.Value!.Amount}.");

Console.ReadLine();
return;

IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyStore.db");
    var connectionString = $"Data Source={dbPath}";

    services
        .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlite(connectionString)
            .UseApplicationServiceProvider(sp)
            .Options);

    services.AddDbContext<MyStoreDbContext>(options =>
    {
        options.UseSqlite(connectionString);
        options.EnableSensitiveDataLogging(); // For debugging
        options.LogTo(Console.WriteLine, LogLevel.Information); // For debugging
    });

    services.AddOpenCqrs(typeof(Program));
    services.AddOpenCqrsEventSourcing(typeof(Program));
    services.AddOpenCqrsEntityFrameworkCore<MyStoreDbContext>();
    services.AddOpenCqrsFluentValidation();

    return services.BuildServiceProvider();
}

static async Task CreateDatabase(IServiceProvider serviceProvider)
{
    try
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MyStoreDbContext>();

        Console.WriteLine("Creating database...");

        var connectionString = context.Database.GetConnectionString();
        if (connectionString != null && connectionString.Contains("Data Source="))
        {
            var dataSourceStart = connectionString.IndexOf("Data Source=", StringComparison.Ordinal) + "Data Source=".Length;
            var dataSourceEnd = connectionString.IndexOf(';', dataSourceStart);
            var dbFilePath = dataSourceEnd > 0
                ? connectionString.Substring(dataSourceStart, dataSourceEnd - dataSourceStart)
                : connectionString.Substring(dataSourceStart);

            dbFilePath = dbFilePath.Trim();

            var directory = Path.GetDirectoryName(dbFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Console.WriteLine($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }

            Console.WriteLine($"Database path: {dbFilePath}");
        }

        await context.Database.EnsureCreatedAsync();

        Console.WriteLine("Database created successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to create database: {ex.Message}");
        throw;
    }
}
