namespace Memoria.Web.Data;

/// <summary>
/// What start-up says about each service's store.
/// </summary>
public static class StoreLog
{
    /// <summary>
    /// One line per service: which connection string it opened and with which engine, or why it
    /// opened none. A warning for the latter, since a service nobody can read is worth a second
    /// look and not a failure — the manifest may simply be ahead of the configuration.
    /// </summary>
    public static void LogStores(this ILogger logger, ServiceStores stores)
    {
        var all = stores.All();

        if (all.Count == 0)
        {
            logger.LogInformation("No services are installed, so no store is opened.");
            return;
        }

        foreach (var store in all)
        {
            if (store.Database is { } database)
            {
                logger.LogInformation(
                    "Service {Service} reads connection string {ConnectionString} with {Provider}.",
                    store.Service.Name, store.Connection.Name, NamedConnection.ProviderName(database.Provider));
            }
            else
            {
                logger.LogWarning(
                    "Service {Service} names connection string {ConnectionString}, which is {State}.",
                    store.Service.Name, store.Connection.Name, store.Connection.Description);
            }
        }
    }
}
