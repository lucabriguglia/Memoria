using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.AppendRoutine;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// Puts the DCB append routine in an EF Core migration, for databases whose schema is managed with
/// migrations.
/// </summary>
/// <remarks>
/// <para>
/// A migration creates the store's tables from the model, but a routine is not part of the model, so
/// it has to be added by hand once: in the migration that creates the tables, or a migration of its
/// own. Both helpers do nothing on engines whose appends go through Entity Framework Core, so the
/// same migration runs anywhere.
/// </para>
/// <para>
/// The routine is generated from the migration's target model when the migration runs, so it fits
/// however the context maps the store's tables. A later Memoria version that changes the routine
/// ships it under a new name; it then needs a new migration calling the helper again.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public partial class InstallDcbAppendRoutine : Migration
/// {
///     protected override void Up(MigrationBuilder migrationBuilder) =>
///         migrationBuilder.CreateDcbAppendRoutine(TargetModel);
///
///     protected override void Down(MigrationBuilder migrationBuilder) =>
///         migrationBuilder.DropDcbAppendRoutine(TargetModel);
/// }
/// </code>
/// </example>
public static class DcbAppendRoutineMigrationBuilderExtensions
{
    /// <summary>Creates or replaces the DCB append routine for the migration's target model.</summary>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="targetModel">The migration's <c>TargetModel</c>.</param>
    /// <returns>The migration builder, for chaining.</returns>
    public static MigrationBuilder CreateDcbAppendRoutine(this MigrationBuilder migrationBuilder, IModel? targetModel)
    {
        if (targetModel is not null && DcbAppendRoutine.For(targetModel, migrationBuilder.ActiveProvider) is { } routine)
        {
            migrationBuilder.Sql(routine.Definition);
        }

        return migrationBuilder;
    }

    /// <summary>Drops the DCB append routine for the migration's target model, if it exists.</summary>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="targetModel">The migration's <c>TargetModel</c>.</param>
    /// <returns>The migration builder, for chaining.</returns>
    /// <remarks>
    /// Only the routine for this mapping: another context's routine in the same database, under a name
    /// of its own, is left alone.
    /// </remarks>
    public static MigrationBuilder DropDcbAppendRoutine(this MigrationBuilder migrationBuilder, IModel? targetModel)
    {
        if (targetModel is not null && DcbAppendRoutine.For(targetModel, migrationBuilder.ActiveProvider) is { } routine)
        {
            migrationBuilder.Sql(routine.Drop);
        }

        return migrationBuilder;
    }
}
