using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Data;

public sealed class MyStoreDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DomainDbContext(options, timeProvider, httpContextAccessor);
