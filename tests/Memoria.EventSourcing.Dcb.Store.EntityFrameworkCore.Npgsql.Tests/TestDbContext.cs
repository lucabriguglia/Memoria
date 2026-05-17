using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

internal sealed class TestDbContext(DbContextOptions options) : DcbDbContext(options);
