using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Web.Extensibility;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>
/// Runs the list page's own queries once at start-up, so nobody's first page load pays for building
/// the model and compiling them.
/// </summary>
/// <remarks>
/// EF Core builds its model and compiles each distinct query the first time it meets it, and Npgsql
/// opens its first connection then too — close to a second of work, which whoever opens the first
/// page that reads anything would otherwise pay.
/// </remarks>
public static class StoreWarmUp
{
    /// <summary>
    /// Warms the store behind the application, without holding up its start.
    /// </summary>
    /// <param name="app">The application, for the scope the warming reads in and its logger.</param>
    /// <param name="database">The store the tool was pointed at.</param>
    /// <remarks>
    /// In the background so the application starts serving straight away, and quietly, because a
    /// store that cannot be reached is the page's problem to report rather than a reason not to
    /// start.
    /// Only the relational stores are warmed. The warming query reads the dynamic consistency
    /// boundary tables, which a Cosmos store does not have — there would be nothing to compile and
    /// no context to ask.
    /// </remarks>
    public static void WarmInBackground(this WebApplication app)
    {
        // Each reachable relational store, one after the other: a Cosmos store has no boundary
        // tables to warm, and an unreachable one has nothing to open.
        var stores = app.Services.GetRequiredService<ServiceStores>().All()
            .Where(store => store.Reachable && store.Database!.Provider is not DatabaseProvider.Cosmos)
            .ToList();

        if (stores.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            foreach (var store in stores)
            {
                await Warm(app, store);
            }
        });
    }

    /// <summary>
    /// Runs the list page's own query once.
    /// </summary>
    /// <remarks>
    /// The real query rather than a stand-in: EF compiles each distinct shape separately, so a
    /// simpler warming query builds the model but leaves the page's own plan to be compiled when it
    /// is asked for. Both orders and the filtered form are warmed alongside it, since those are what
    /// the column headings and the filter box lead to. Failure is logged and dropped — the store
    /// being unreachable is something the pages report, not a reason to hold up start-up.
    /// </remarks>
    private static async Task Warm(WebApplication app, ServiceStore serviceStore)
    {
        try
        {
            using var scope = app.Services.CreateScope();

            // The scope is put inside the service the way a request under it would be, so the
            // context it resolves is that service's, over that service's store and bindings.
            var catalogue = app.Services.GetRequiredService<DomainTypeRegistry>().Current;
            scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(serviceStore.Service, catalogue);

            var store = scope.ServiceProvider.GetRequiredService<IDcbDbContext>();
            var types = catalogue.For(serviceStore.Service);
            var totals = serviceStore.Totals;

            // Either kind will do. Both pages run the same two queries, and the kind rides in as a
            // parameter rather than as part of the SQL, so whichever is warmed first warms the other's
            // pages too — and an application with only projections uploaded is still warmed.
            foreach (var kind in Enum.GetValues<DcbModelKind>())
            {
                foreach (var model in types.Models(kind))
                {
                    var shape = DomainTypeDescriber.Describe(model, types.Identifiers(kind)).Identifiers
                        .Select(IdentifierShape.Of)
                        .FirstOrDefault(candidate => candidate is not null);

                    if (shape is null)
                    {
                        continue;
                    }

                    // Read off the attribute rather than through the framework's own lookup, which
                    // throws for a type carrying none. A model that cannot be stored has no rows to
                    // warm, so it is passed over rather than allowed to fail the warming of the rest.
                    if (DomainTypeDescriber.BindingOf(model)?.Key is not { } modelType)
                    {
                        continue;
                    }

                    IReadOnlyList<Instance> listed = [];

                    foreach (var sort in Enum.GetValues<InstanceSort>())
                    {
                        foreach (var tag in new string?[] { null, "warm" })
                        {
                            // The three ways the list narrows: to one identifier's rows, to one model's
                            // whatever addresses them, and to every model of the kind. Each leaves out
                            // a different clause, so each is its own query for EF to compile — and the
                            // page opens on the widest of them, which would otherwise be the one left
                            // to compile on arrival.
                            foreach (var (narrowed, narrowedShape) in new (string?, IdentifierShape?)[]
                                     {
                                         (modelType, shape), (modelType, null), (null, null)
                                     })
                            {
                                var page = await IdentifierInstances.Page(store, kind, narrowed,
                                    narrowedShape, tag, sort, descending: true, page: 1,
                                    size: InstanceQuery.DefaultPageSize, totals);

                                // Only the shaped read unfolds a boundary into the values an identifier
                                // is built from, which is what the detail read below needs.
                                if (narrowedShape is not null)
                                {
                                    listed = listed.Count > 0 ? listed : page.Rows;
                                }
                            }
                        }
                    }

                    // The detail page reads the same table, but addressed by one row's key rather
                    // than by the shape of a boundary. That is a distinct query for EF to compile, so
                    // warming the list alone would leave it to be compiled on the first row anyone
                    // opens.
                    if (listed.FirstOrDefault() is { } instance &&
                        IdentifierFactory.Create(shape.Identifier,
                            instance.Values.ToDictionary(
                                value => value.Key, value => (string?)value.Value)).Instance is { } identifier)
                    {
                        await ModelReader.Load(store, model, kind, identifier);
                    }

                    app.Logger.LogInformation("Store of service {Service} warmed on {Model}.", serviceStore.Service.Name, model.Name);
                    return;
                }
            }

            // Nothing uploaded yet, so there is no real query to run. The model is still worth building.
            await store.DcbSnapshots.AsNoTracking().Select(snapshot => snapshot.Id).FirstOrDefaultAsync();

            app.Logger.LogInformation("Store of service {Service} warmed.", serviceStore.Service.Name);
        }
        catch (Exception exception)
        {
            app.Logger.LogWarning(exception,
                "Could not warm the store of service {Service}. The first page that reads it will be slower.",
                serviceStore.Service.Name);
        }
    }
}
