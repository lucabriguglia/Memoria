using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

public abstract class TestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private protected readonly TestDbContext DbContext;
    private protected readonly EntityFrameworkCoreDcbStore Store;

    protected TestBase()
    {
        TestTypeBindings.Register();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbContext = new TestDbContext(options);
        DbContext.Database.EnsureCreated();

        Store = new EntityFrameworkCoreDcbStore(DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
