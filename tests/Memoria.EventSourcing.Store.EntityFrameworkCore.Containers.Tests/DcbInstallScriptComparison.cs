using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Compares the schema the DCB install script produces against the schema the DCB model produces.
/// </summary>
/// <remarks>
/// Same argument as <see cref="InstallScriptComparison"/>: a shipped script that drifts from
/// <c>OnModelCreating</c> stands up a database the store then fails against for reasons nobody can
/// see. Separate from that type because the tables and the context differ.
/// </remarks>
public static class DcbInstallScriptComparison
{
    /// <summary>The DCB store's tables, in an order safe for dropping — dependants first.</summary>
    public static readonly string[] TablesInDropOrder =
    [
        "DcbEventTags",
        "DcbTagHeads",
        "DcbSnapshots",
        "DcbEvents"
    ];

    public delegate Task<IReadOnlyList<string>> ReadIndexes(DbContext dbContext, string tableName);

    /// <summary>
    /// A single comparable description of every table: its columns with their engine types and
    /// collations, and its indexes including primary keys.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DescribeAsync(DbContext dbContext, ReadIndexes readIndexes)
    {
        var description = new List<string>();

        foreach (var table in TablesInDropOrder.Order(StringComparer.Ordinal))
        {
            var columns = await ColumnMetadata.ReadAsync(dbContext, table);

            foreach (var column in columns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                description.Add($"{table}.{column.Key} {column.Value}");
            }

            // Collation is the reason this store has a correctness stake in the schema at all, and
            // INFORMATION_SCHEMA.COLUMNS does not carry it, so it is read separately.
            foreach (var collation in await DcbCollationMetadata.ReadAsync(dbContext, table))
            {
                description.Add($"{table}.{collation.Key} collate {collation.Value}");
            }

            foreach (var index in await readIndexes(dbContext, table))
            {
                description.Add($"{table} index {index}");
            }
        }

        return description;
    }
}
