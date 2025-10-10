using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Data;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests;

public static class Shared
{
    public static TestDbContext CreateTestDbContext() =>
        new(CreateContextOptions(), new FakeTimeProvider(), CreateHttpContextAccessor());

    public static DbContextOptions<DomainDbContext> CreateContextOptions()
    {
        var builder = new DbContextOptionsBuilder<DomainDbContext>();
        builder.UseInMemoryDatabase("Memoria");
        return builder.Options;
    }

    public static IHttpContextAccessor CreateHttpContextAccessor()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "TestUser")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        context.User = principal;

        httpContextAccessor.HttpContext.Returns(context);
        return httpContextAccessor;
    }

    public static IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor createHttpContextAccessor)
    {
        var dbContext = new TestDbContext(CreateContextOptions(), timeProvider, CreateHttpContextAccessor());
        return new EntityFrameworkCoreDomainService(dbContext);
    }

    public static IDomainService CreateDomainService(IDomainDbContext domainDbContext)
    {
        return new EntityFrameworkCoreDomainService(domainDbContext);
    }
}
