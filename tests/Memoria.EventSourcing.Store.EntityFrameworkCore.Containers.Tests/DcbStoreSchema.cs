using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Builds the DCB store's context against a real provider. The model under test is entirely the one
/// the store configures — these contexts add nothing of their own.
/// </summary>
internal static class DcbStoreSchema
{
    public static TestDbContext OnSqlServer(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseSqlServer(connectionString), interceptors);

    public static TestDbContext OnPostgreSql(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseNpgsql(connectionString), interceptors);

    private static TestDbContext Build(
        Func<DbContextOptionsBuilder<DcbDbContext>, DbContextOptionsBuilder> useProvider,
        IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<DcbDbContext>();
        useProvider(builder);
        builder.AddInterceptors(interceptors);

        return new TestDbContext(builder.Options, TimeProvider.System, new StubHttpContextAccessor());
    }
}

/// <summary>
/// The audit interceptor asks for the current user; these tests assert on schema and concurrency,
/// not on who wrote.
/// </summary>
internal sealed class StubHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
}
