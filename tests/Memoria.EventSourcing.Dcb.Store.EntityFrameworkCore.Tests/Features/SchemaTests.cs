using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

/// <summary>
/// The relational model, built against SQL Server without connecting to one.
/// </summary>
/// <remarks>
/// The in-memory provider models neither collation nor composite keys faithfully, so these assert
/// against a real relational model instead. Building the model needs no database; the container
/// tests later verify the same shape against a running engine.
/// </remarks>
public class SchemaTests
{
    private static TestDbContext SqlServerContext() =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlServer("Server=none;Database=none;Trusted_Connection=True;")
                .Options,
            new FakeTimeProvider(),
            Substitute.For<IHttpContextAccessor>());

    [Fact]
    public void The_tag_column_is_case_sensitive_on_sql_server()
    {
        // Tags compare ordinally in .NET. SQL Server's default collation is case-insensitive, which
        // would fold seat:A1 into seat:a1 and silently widen every boundary using them.
        using var context = SqlServerContext();

        // Collation is design-time metadata, stripped from the read-optimised runtime model, so it
        // has to be read from the design-time model the migration tooling would use.
        var tag = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(DcbEventTagEntity))!
            .FindProperty(nameof(DcbEventTagEntity.Tag))!;

        tag.GetCollation().Should().Be("SQL_Latin1_General_CP1_CS_AS");
    }

    [Fact]
    public void The_tag_table_is_keyed_tag_first()
    {
        // Every read narrows by tag before position, so the key is also the serving index.
        using var context = SqlServerContext();

        var key = context.Model.FindEntityType(typeof(DcbEventTagEntity))!.FindPrimaryKey()!;

        key.Properties.Select(property => property.Name)
            .Should().ContainInOrder(nameof(DcbEventTagEntity.Tag), nameof(DcbEventTagEntity.Position));
    }

    [Fact]
    public void The_log_carries_no_index_the_primary_keys_do_not_already_serve()
    {
        // Every read resolves its boundary through the DcbEventTags primary key and then reaches
        // DcbEvents by position, which is its primary key. A secondary index on the log is therefore
        // maintained on every append and chosen by nothing — and an index on EventType is worse than
        // unused, because the optimiser prefers it to the tag semi-join and loses.
        //
        // Measured on SQL Server 2022 over 200,000 events / 400,000 tag rows / 41,761 distinct tags,
        // across 540 reads covering every shape the store offers: IX_DcbEvents_CreatedDate was used
        // zero times, and IX_DcbEvents_EventType took a type-filtered read from 4ms to 210ms.
        //
        // A boundary is the consistency boundary of one decision and so is narrow by construction,
        // which is why the tag predicate beats a filter on type or date every time. Re-adding an
        // index here needs a measurement saying otherwise.
        using var context = SqlServerContext();

        var model = context.GetService<IDesignTimeModel>().Model;

        model.FindEntityType(typeof(DcbEventEntity))!.GetIndexes().Should().BeEmpty();

        // The tag table's one index is the foreign key's, created by convention rather than declared.
        // It earns nothing here either, but removing it costs a replacement convention set on the
        // context — see DcbEventTagEntityConfiguration.
        model.FindEntityType(typeof(DcbEventTagEntity))!.GetIndexes()
            .Should().ContainSingle(index => index.Properties.Single().Name == nameof(DcbEventTagEntity.Position));
    }

    [Fact]
    public void The_tag_key_fits_inside_the_sql_server_index_key_limit()
    {
        // nvarchar(255) is 510 bytes, plus 8 for the bigint: 518 against a 900-byte limit. 1.7.0
        // removed the last table that exceeded it; this one must not reintroduce the problem.
        using var context = SqlServerContext();

        var entityType = context.Model.FindEntityType(typeof(DcbEventTagEntity))!;
        var maxLength = entityType.FindProperty(nameof(DcbEventTagEntity.Tag))!.GetMaxLength();

        maxLength.Should().Be(255);
        (maxLength!.Value * 2 + sizeof(long)).Should().BeLessThan(900);
    }

    [Fact]
    public void Events_are_keyed_on_a_store_assigned_position()
    {
        using var context = SqlServerContext();

        var entityType = context.Model.FindEntityType(typeof(DcbEventEntity))!;
        var position = entityType.FindProperty(nameof(DcbEventEntity.Position))!;

        entityType.FindPrimaryKey()!.Properties.Should().ContainSingle()
            .Which.Name.Should().Be(nameof(DcbEventEntity.Position));
        position.ValueGenerated.Should().Be(ValueGenerated.OnAdd);
        position.ClrType.Should().Be<long>();
    }

    [Fact]
    public void Deleting_an_event_takes_its_tags_with_it()
    {
        using var context = SqlServerContext();

        var foreignKey = context.Model.FindEntityType(typeof(DcbEventTagEntity))!
            .GetForeignKeys().Should().ContainSingle().Subject;

        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void The_two_tables_are_named_for_the_consistency_model_they_serve()
    {
        // They may share a DbContext with the streamed store's three tables.
        using var context = SqlServerContext();

        context.Model.FindEntityType(typeof(DcbEventEntity))!.GetTableName().Should().Be("DcbEvents");
        context.Model.FindEntityType(typeof(DcbEventTagEntity))!.GetTableName().Should().Be("DcbEventTags");
    }
}
