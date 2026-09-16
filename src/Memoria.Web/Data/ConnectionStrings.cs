namespace Memoria.Web.Data;

/// <summary>
/// The connection strings the tool has, by name. A service names one; the configuration holds it;
/// this is where the two meet.
/// </summary>
/// <remarks>
/// Which engine opens each string is read the way <see cref="DatabaseConnection"/> reads it: off
/// the string, or from a setting where the string could be more than one. The setting is per
/// name, <c>Databases:{name}:Provider</c>, and so are the Cosmos database and container names,
/// <c>Databases:{name}:Cosmos:DatabaseName</c> and <c>ContainerName</c>. The one string the tool
/// has always read, <c>Memoria</c>, keeps its older <c>Database:Provider</c> and
/// <c>Database:Cosmos:*</c> as well, so a single-store configuration from before services keeps
/// working unchanged.
/// </remarks>
public static class ConnectionStrings
{
    /// <summary>The configuration section the strings live under.</summary>
    public const string Section = "ConnectionStrings";

    /// <summary>
    /// What the configuration says about the connection string of that name: nothing, a store an
    /// engine opens, or a string that could not be read and why.
    /// </summary>
    /// <remarks>
    /// Never throws: a service naming a string the configuration lacks, or one it cannot read, is
    /// listed as unreachable with the reason, and the person reading the page is the one who can
    /// fix it. Start-up is stricter — see <see cref="Validate"/>.
    /// </remarks>
    public static NamedConnection Named(IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new NamedConnection(name, Configured: false, Database: null, Problem: null, Cosmos: null);
        }

        try
        {
            var database = DatabaseConnection.Of(connectionString, ProviderSetting(configuration, name), name);
            var cosmos = database.Provider is DatabaseProvider.Cosmos
                ? CosmosStore.Of(name, database, configuration)
                : null;

            return new NamedConnection(name, Configured: true, database, Problem: null, cosmos);
        }
        catch (InvalidOperationException exception)
        {
            return new NamedConnection(name, Configured: true, Database: null, exception.Message, Cosmos: null);
        }
    }

    /// <summary>
    /// Refuses a configuration holding a connection string that cannot be read, whatever it is
    /// called: a string nobody can open is a mistake best found before a page asks for it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A configured string could not be read.</exception>
    public static void Validate(IConfiguration configuration)
    {
        foreach (var entry in configuration.GetSection(Section).GetChildren())
        {
            if (Named(configuration, entry.Key).Problem is { } problem)
            {
                throw new InvalidOperationException(problem);
            }
        }
    }

    /// <summary>
    /// The provider setting for a name: its own, or the older unnamed one for the string the tool
    /// has always read.
    /// </summary>
    private static string? ProviderSetting(IConfiguration configuration, string name) =>
        configuration[$"Databases:{name}:Provider"]
        ?? (name == DatabaseConnection.Name ? configuration[DatabaseConnection.Setting] : null);
}

/// <summary>
/// One named connection string as the configuration holds it.
/// </summary>
/// <param name="Name">The name a service reads it by.</param>
/// <param name="Configured">Whether an entry of that name is under <c>ConnectionStrings</c>.</param>
/// <param name="Database">The store and the engine that opens it, when configured and readable.</param>
/// <param name="Problem">Why it could not be read, when configured and it could not be.</param>
/// <param name="Cosmos">The database and container names, when the engine is Cosmos DB.</param>
public sealed record NamedConnection(
    string Name, bool Configured, DatabaseConnection? Database, string? Problem, CosmosStore? Cosmos)
{
    /// <summary>What the settings sheet and the home page say beside the name.</summary>
    public string Description => this switch
    {
        { Configured: false } => "not configured",
        { Database: { } database } => $"configured, {ProviderName(database.Provider)}",
        _ => $"configured, but it could not be read: {Problem}"
    };

    /// <summary>What each engine is called in a sentence.</summary>
    public static string ProviderName(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Npgsql => "PostgreSQL",
        DatabaseProvider.SqlServer => "SQL Server",
        DatabaseProvider.Sqlite => "SQLite",
        DatabaseProvider.Cosmos => "Cosmos DB",
        _ => provider.ToString()
    };
}
