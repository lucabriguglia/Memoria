using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Compares the schema an install script produces against the schema the EF model produces.
/// </summary>
/// <remarks>
/// A shipped install script that quietly drifts from <c>OnModelCreating</c> is worse than no script
/// at all: it would stand up a database the store then fails against for reasons nobody can see. The
/// only way to keep the two honest is to build both and compare.
/// </remarks>
public static class InstallScriptComparison
{
    /// <summary>The store's tables, in an order safe for dropping — dependants first.</summary>
    public static readonly string[] TablesInDropOrder =
    [
        "events",
        "DomainAggregates",
        "DomainProjections"
    ];

    public delegate Task<IReadOnlyList<string>> ReadIndexes(RelationalTestDbContext dbContext, string tableName);

    /// <summary>
    /// A single comparable description of every table: its columns with their engine types, and its
    /// indexes including primary keys.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DescribeAsync(RelationalTestDbContext dbContext,
        ReadIndexes readIndexes)
    {
        var description = new List<string>();

        foreach (var table in TablesInDropOrder.Order(StringComparer.Ordinal))
        {
            var columns = await ColumnMetadata.ReadAsync(dbContext, table);

            foreach (var column in columns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                description.Add($"{table}.{column.Key} {column.Value}");
            }

            foreach (var index in await readIndexes(dbContext, table))
            {
                description.Add($"{table} index {index}");
            }
        }

        return description;
    }
}
