using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Data;

public sealed class BoxOfficeDbContext(DbContextOptions options) : DcbDbContext(options);
