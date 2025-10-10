// See https://aka.ms/new-console-template for more information

using Memoria;
using Memoria.Extensions;
using Memoria.Messaging.RabbitMq.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenCqrs;
using OpenCqrs.Examples.Messaging.RabbitMq.Commands;

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

    const string connectionString = "amqp://guest:guest@localhost:5672/";

    services.AddOpenCqrs(typeof(Program));
    services.AddOpenCqrsRabbitMq(connectionString);

    return services.BuildServiceProvider();
}
