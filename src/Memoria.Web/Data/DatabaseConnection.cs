using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>
/// The store this application was pointed at: the connection string it was given, and the provider
/// that opens it.
/// </summary>
/// <param name="Provider">The engine the connection string names.</param>
/// <param name="ConnectionString">The connection string, unchanged.</param>
/// <remarks>
/// The tool is pointed at a store somebody else created, so which engine that store is in is
/// something it is told rather than something it chooses. The connection string is the only thing
/// it is always given, and most of them say plainly which engine they are for: a keyword only one
/// provider takes settles it. When none does, the <c>Database:Provider</c> setting is asked for
/// rather than one of the three being picked — a guess would be a connection failure minutes later
/// against a store that is perfectly reachable, and the reader would have no reason to suspect the
/// provider.
/// </remarks>
public sealed record DatabaseConnection(DatabaseProvider Provider, string ConnectionString)
{
    /// <summary>The setting that says which provider, when the connection string does not.</summary>
    public const string Setting = "Database:Provider";

    /// <summary>The name of the connection string this application reads.</summary>
    public const string Name = "Memoria";

    /// <summary>
    /// What each provider may be called in the setting. Written the way each one's own
    /// documentation writes it, plus the names people reach for — spaces, hyphens and underscores
    /// are taken out before the match, so <c>SQL Server</c> and <c>sql_server</c> both arrive here
    /// as one word.
    /// </summary>
    private static readonly Dictionary<string, DatabaseProvider> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["npgsql"] = DatabaseProvider.Npgsql,
        ["postgres"] = DatabaseProvider.Npgsql,
        ["postgresql"] = DatabaseProvider.Npgsql,
        ["sqlserver"] = DatabaseProvider.SqlServer,
        ["mssql"] = DatabaseProvider.SqlServer,
        ["sqlite"] = DatabaseProvider.Sqlite,
        ["cosmos"] = DatabaseProvider.Cosmos,
        ["cosmosdb"] = DatabaseProvider.Cosmos,
        ["azurecosmosdb"] = DatabaseProvider.Cosmos
    };

    /// <summary>Keywords Npgsql takes and the other two do not.</summary>
    /// <remarks>
    /// <c>Server</c> is not among them although Npgsql takes it: SQL Server takes it too, and a
    /// keyword shared with another provider says nothing. The same goes for <c>Database</c>,
    /// <c>User Id</c> and <c>Password</c>, which all three take.
    /// </remarks>
    private static readonly string[] NpgsqlKeywords =
    [
        "host", "port", "username", "searchpath", "includeerrordetail", "noresetonclose",
        "maximumpoolsize", "minimumpoolsize", "sslmode", "targetsessionattributes"
    ];

    /// <summary>Keywords SQL Server takes and the other two do not.</summary>
    private static readonly string[] SqlServerKeywords =
    [
        "initialcatalog", "integratedsecurity", "trustedconnection", "trustservercertificate",
        "encrypt", "multipleactiveresultsets", "attachdbfilename", "applicationintent",
        "userinstance", "authentication", "packetsize", "workstationid", "failoverpartner",
        "multisubnetfailover", "connectretrycount", "connectretryinterval", "currentlanguage",
        "maxpoolsize", "minpoolsize", "networklibrary"
    ];

    /// <summary>Keywords SQLite takes and the other two do not.</summary>
    private static readonly string[] SqliteKeywords =
        ["mode", "cache", "foreignkeys", "recursivetriggers"];

    /// <summary>Keywords Cosmos takes and no relational provider does.</summary>
    /// <remarks>
    /// Cosmos names an account rather than a server, which is why none of these is shared: the
    /// other three have nothing to say about an account endpoint or an account key.
    /// </remarks>
    private static readonly string[] CosmosKeywords =
        ["accountendpoint", "accountkey", "authkeyorresourcetoken"];

    /// <summary>What a SQLite database is usually called, for a string that names only a file.</summary>
    private static readonly string[] SqliteExtensions =
        [".db", ".db3", ".sqlite", ".sqlite3", ".s3db"];

    /// <summary>
    /// Where the source in a connection string is written, which is the one value worth reading:
    /// the rest are credentials and tuning, and say nothing about the engine.
    /// </summary>
    private static readonly string[] SourceKeywords = ["datasource", "filename", "server", "host"];

    /// <summary>
    /// Works out which provider opens the connection string this application was given.
    /// </summary>
    /// <param name="connectionString">The <c>Memoria</c> connection string from configuration.</param>
    /// <param name="configured">The <c>Database:Provider</c> setting, or null when it is not set.</param>
    /// <returns>The connection string and the provider that opens it.</returns>
    /// <exception cref="InvalidOperationException">
    /// The connection string is missing or unreadable, the setting names a provider this tool does
    /// not have, no provider can be read off the string and the setting is not set to settle it, or
    /// the string asks for a SQLite database that would not outlive the connection.
    /// </exception>
    /// <summary>
    /// The setting that settles the provider for a named string: the older unnamed one for the
    /// string the tool has always read, and one under the name for every other.
    /// </summary>
    public static string ProviderSettingFor(string name) =>
        name == Name ? Setting : $"Databases:{name}:Provider";

    public static DatabaseConnection Of(string? connectionString, string? configured, string name = Name)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is not configured in appsettings.json.");
        }

        var values = Values(connectionString, name);

        // The setting first, and it is not checked against the string: it is the way out of a
        // string this cannot read, so second-guessing it would close the door it opens.
        var provider = string.IsNullOrWhiteSpace(configured) ? Infer(values, name) : Configured(configured, name);

        if (provider is DatabaseProvider.Sqlite && OnlyLivesAsLongAsTheConnection(values))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' asks for a SQLite database held in memory, which lives " +
                "only as long as the connection that opened it. This tool opens a connection per " +
                "unit of work, so it would find an empty store rather than the one that was seeded. " +
                "Point it at a database file.");
        }

        return new DatabaseConnection(provider, connectionString);
    }

    /// <summary>
    /// Puts this connection's provider on a context's options.
    /// </summary>
    /// <param name="builder">The options being built.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Takes the base builder rather than a closed one because both shapes registered in
    /// <c>Program</c> reach it: the two stores' options are built directly, and the two contexts
    /// are registered through <c>AddDbContext</c>, which hands over the base builder.
    /// </remarks>
    public DbContextOptionsBuilder Apply(DbContextOptionsBuilder builder) => Provider switch
    {
        DatabaseProvider.Npgsql => builder.UseNpgsql(ConnectionString),
        DatabaseProvider.SqlServer => builder.UseSqlServer(ConnectionString),
        DatabaseProvider.Sqlite => builder.UseSqlite(ConnectionString),
        // Said here rather than left to fail further in. A Cosmos store is read through its own
        // SDK, so there is no context to open and nothing calls this — an unhandled enum would
        // reach whoever added the arm, and this reaches whoever wired the store.
        DatabaseProvider.Cosmos => throw new InvalidOperationException(
            "A Cosmos store is not opened through a DbContext. Its documents are read through the " +
            "Cosmos SDK, so nothing here has a provider to apply."),
        _ => throw new ArgumentOutOfRangeException(nameof(Provider), Provider,
            "There is no provider for this engine.")
    };

    /// <summary>
    /// The provider the setting names.
    /// </summary>
    private static DatabaseProvider Configured(string configured, string name) =>
        Named.TryGetValue(Normalised(configured), out var provider)
            ? provider
            : throw new InvalidOperationException(
                $"'{configured}' is not a database provider this tool has. Set {ProviderSettingFor(name)} to " +
                "Npgsql, SqlServer, Sqlite or Cosmos, or leave it out to read the provider off the " +
                "connection string.");

    /// <summary>
    /// The provider the connection string itself names, by a keyword or a source only one of the
    /// three would take.
    /// </summary>
    /// <remarks>
    /// Each provider is looked for on its own and one has to answer alone. A string carrying
    /// signals for two of them is as unreadable as one carrying none — both are cases where
    /// choosing would be choosing arbitrarily, and both are answered by asking for the setting.
    /// </remarks>
    private static DatabaseProvider Infer(IReadOnlyDictionary<string, string> values, string name)
    {
        var source = SourceKeywords
            .Select(keyword => values.GetValueOrDefault(keyword))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        var candidates = new List<DatabaseProvider>();

        if (Carries(values, NpgsqlKeywords))
        {
            candidates.Add(DatabaseProvider.Npgsql);
        }

        // A source SQL Server alone would be given: a local instance, an address it reaches over
        // its own protocol prefix, or the managed service's own domain.
        if (Carries(values, SqlServerKeywords) ||
            source.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ||
            source.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(DatabaseProvider.SqlServer);
        }

        if (Carries(values, SqliteKeywords) || IsDatabaseFile(source))
        {
            candidates.Add(DatabaseProvider.Sqlite);
        }

        if (Carries(values, CosmosKeywords))
        {
            candidates.Add(DatabaseProvider.Cosmos);
        }

        return candidates.Count == 1
            ? candidates[0]
            : throw new InvalidOperationException(
                $"The provider for connection string '{name}' could not be read off it: " +
                Why(values, candidates.Count) +
                $" Set {ProviderSettingFor(name)} to Npgsql, SqlServer, Sqlite or Cosmos.");
    }

    /// <summary>
    /// Why a connection string named no single provider.
    /// </summary>
    /// <remarks>
    /// Three ways to fail and only two of them are the same problem. A string carrying signals for
    /// two providers and a string carrying only keywords they share are both cases where choosing
    /// would be choosing arbitrarily. A string carrying keywords none of them takes is not that: it
    /// is a string for something else entirely, and telling its reader that several providers take
    /// its keywords would send them looking for an ambiguity that is not there.
    /// </remarks>
    private static string Why(IReadOnlyDictionary<string, string> values, int candidates) =>
        candidates > 1
            ? "it carries keywords for more than one provider."
            : values.Keys.Any(Known.Contains)
                ? "every keyword in it is one that more than one provider takes."
                : "nothing in it is a keyword any provider this tool has would take.";

    /// <summary>Every keyword this tool recognises, whoever takes it.</summary>
    private static readonly HashSet<string> Known =
    [
        .. NpgsqlKeywords, .. SqlServerKeywords, .. SqliteKeywords, .. CosmosKeywords,
        .. SourceKeywords, "database", "userid", "password", "uid", "pwd"
    ];

    /// <summary>
    /// Whether the source names a database file, which is the one thing a SQLite connection string
    /// may consist of.
    /// </summary>
    /// <remarks>
    /// Read off the extension rather than off the shape of the path. A directory separator says
    /// nothing on its own: a Windows path to a database file and a named SQL Server instance both
    /// carry one, and a source that could be either is left to the setting to settle.
    /// </remarks>
    private static bool IsDatabaseFile(string source) =>
        SqliteExtensions.Contains(Path.GetExtension(source), StringComparer.OrdinalIgnoreCase) ||
        source.Equals(":memory:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a SQLite connection string asks for a database that is gone when the connection
    /// that opened it closes — either mode of asking for one.
    /// </summary>
    private static bool OnlyLivesAsLongAsTheConnection(IReadOnlyDictionary<string, string> values) =>
        values.GetValueOrDefault("mode", string.Empty)
            .Equals("Memory", StringComparison.OrdinalIgnoreCase) ||
        SourceKeywords.Any(keyword => values.GetValueOrDefault(keyword, string.Empty)
            .Equals(":memory:", StringComparison.OrdinalIgnoreCase));

    private static bool Carries(IReadOnlyDictionary<string, string> values, string[] keywords) =>
        keywords.Any(values.ContainsKey);

    /// <summary>
    /// Reads the connection string into its keywords and their values, with the keywords normalised
    /// so that the spellings a provider accepts for one of them all arrive as the same word.
    /// </summary>
    private static Dictionary<string, string> Values(string connectionString, string name)
    {
        var parsed = new DbConnectionStringBuilder();

        try
        {
            parsed.ConnectionString = connectionString;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' could not be read: {exception.Message}", exception);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string keyword in parsed.Keys)
        {
            values[Normalised(keyword)] = parsed[keyword]?.ToString() ?? string.Empty;
        }

        return values;
    }

    /// <summary>
    /// One keyword or provider name written one way: lower case, and without the spaces, hyphens
    /// and underscores the same word is written with and without.
    /// </summary>
    private static string Normalised(string word) =>
        string.Concat(word.Where(character => character is not (' ' or '-' or '_'))).ToLowerInvariant();
}
