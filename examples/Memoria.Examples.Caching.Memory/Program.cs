// See https://aka.ms/new-console-template for more information

using Memoria;
using Memoria.Caching.Memory.Extensions;
using Memoria.Examples.Caching.Memory.Queries;
using Memoria.Extensions;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = ConfigureServices();

var dispatcher = serviceProvider.GetService<IDispatcher>();

var result = await dispatcher!.Get(new GetSomethingQuery
{
    CacheKey = "my-cache-key",
    CacheTimeInSeconds = 600
});
Console.WriteLine($"Something retrieved. Result: {result.Value}.");

Console.ReadLine();
return;

IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    services.AddMemoria(typeof(Program));
    services.AddMemoriaMemoryCache();

    return services.BuildServiceProvider();
}
