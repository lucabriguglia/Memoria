namespace Memoria.Web.Data;

/// <summary>
/// What the configuration says about one named connection string, in the words the settings page
/// says it in: not there, there and opened by a named engine, or there but unreadable.
/// </summary>
/// <param name="Configured">Whether an entry of that name is under <c>ConnectionStrings</c>.</param>
/// <param name="Provider">The engine that opens it, when it is configured and readable.</param>
/// <param name="Problem">Why it could not be read, when it is configured and could not be.</param>
/// <remarks>
/// A manifest names a connection string; the configuration holds it. Whether the two meet is
/// something only the running instance can say, and the person reading the sheet is the one who
/// can fix a name that meets nothing — so it is said there rather than found out on a page that
/// reads nothing.
/// <para>
/// Which engine is read the way <see cref="DatabaseConnection"/> reads it: off the string, or
/// from a provider setting where the string could be more than one. The setting is per name,
/// <c>Databases:{name}:Provider</c>; the one string the tool has always read, <c>Memoria</c>,
/// keeps its older <c>Database:Provider</c> as well.
/// </para>
/// </remarks>
public sealed record ConnectionStringState(bool Configured, DatabaseProvider? Provider, string? Problem)
{
    /// <summary>Reads what the configuration says about the connection string of that name.</summary>
    public static ConnectionStringState Of(IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new ConnectionStringState(Configured: false, Provider: null, Problem: null);
        }

        var provider = configuration[$"Databases:{name}:Provider"]
                       ?? (name == DatabaseConnection.Name ? configuration[DatabaseConnection.Setting] : null);

        try
        {
            return new ConnectionStringState(
                Configured: true, DatabaseConnection.Of(connectionString, provider).Provider, Problem: null);
        }
        catch (InvalidOperationException exception)
        {
            return new ConnectionStringState(Configured: true, Provider: null, exception.Message);
        }
    }

    /// <summary>What the sheet says beside the name.</summary>
    public string Description => this switch
    {
        { Configured: false } => "not configured",
        { Provider: { } provider } => $"configured, {Named(provider)}",
        _ => $"configured, but it could not be read: {Problem}"
    };

    /// <summary>What each engine is called in a sentence.</summary>
    public static string Named(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Npgsql => "PostgreSQL",
        DatabaseProvider.SqlServer => "SQL Server",
        DatabaseProvider.Sqlite => "SQLite",
        DatabaseProvider.Cosmos => "Cosmos DB",
        _ => provider.ToString()
    };
}
