using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Reads the collation an engine actually applied to each text column.
/// </summary>
/// <remarks>
/// The DCB store's correctness depends on this: tags compare ordinally in .NET, so a case-insensitive
/// column would fold <c>seat:A1</c> and <c>seat:a1</c> into one row and quietly widen every boundary
/// naming them. Neither the EF model nor <c>INFORMATION_SCHEMA.COLUMNS</c> proves what the engine
/// did, so it is read back from the engine's own catalogue.
/// </remarks>
public static class DcbCollationMetadata
{
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(DbContext dbContext, string tableName)
    {
        var isPostgreSql = dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

        // Both return the column's own collation and fall back to the database default when the
        // column does not override it, so the two engines are described the same way.
        var sql = isPostgreSql
            ? """
              SELECT a.attname,
                     COALESCE(c.collname, (SELECT datcollate FROM pg_database WHERE datname = current_database()))
              FROM pg_attribute a
              JOIN pg_class t ON t.oid = a.attrelid
              LEFT JOIN pg_collation c ON c.oid = a.attcollation
              WHERE t.relname = @tableName AND a.attnum > 0 AND NOT a.attisdropped AND a.attcollation <> 0
              """
            : """
              SELECT COLUMN_NAME, COLLATION_NAME
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = @tableName AND COLLATION_NAME IS NOT NULL
              """;

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var collations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                collations[reader.GetString(0)] = reader.GetString(1);
            }

            return collations;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
