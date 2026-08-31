using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Data;

/// <summary>
/// Nothing to add: deriving from <see cref="DcbDbContext"/> brings the four DCB tables with it.
/// </summary>
public class SchoolDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor);
