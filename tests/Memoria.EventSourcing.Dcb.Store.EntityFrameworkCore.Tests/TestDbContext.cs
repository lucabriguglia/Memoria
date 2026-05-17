using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

internal sealed class TestDbContext(DbContextOptions options) : DcbDbContext(options);
