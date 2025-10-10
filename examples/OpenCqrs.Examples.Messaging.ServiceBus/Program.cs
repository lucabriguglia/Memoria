// See https://aka.ms/new-console-template for more information

using Memoria;
using Memoria.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenCqrs;
using OpenCqrs.Examples.Messaging.ServiceBus.Commands;
using OpenCqrs.Messaging.ServiceBus.Configuration;
using OpenCqrs.Messaging.ServiceBus.Extensions;

var serviceProvider = ConfigureServices();

var dispatcher = serviceProvider.GetService<IDispatcher>();

var sendAndPublishResponse = await dispatcher!.SendAndPublish(new PlaceOrderCommand(CustomerId: Guid.NewGuid(), OrderId: Guid.NewGuid(), Amount: 25m));

Console.WriteLine($"Command Result IsSuccess: {sendAndPublishResponse.CommandResult.IsSuccess}");
Console.WriteLine($"Message Results Success Count: {sendAndPublishResponse.MessageResults.Count(m => m.IsSuccess)}");
Console.WriteLine($"Message Results Failure Count: {sendAndPublishResponse.MessageResults.Count(m => m.IsNotSuccess)}");

Console.ReadLine();
return;

IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

    const string connectionString = "your_service_bus_connection_string";

    services.AddOpenCqrs(typeof(Program));
    services.AddOpenCqrsServiceBus(new ServiceBusOptions { ConnectionString = connectionString });

    return services.BuildServiceProvider();
}
