using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Memoria.Caching.Memory.Tests;

public class MemoryCacheProviderTests
{
    private readonly MemoryCacheProvider _provider;

    public MemoryCacheProviderTests()
    {
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptions());
        var defaultOptions = new Memoria.Caching.Memory.Configuration.MemoryCacheOptions { DefaultCacheTimeInSeconds = 300 };
        var options = Substitute.For<IOptions<Memoria.Caching.Memory.Configuration.MemoryCacheOptions>>();
        options.Value.Returns(defaultOptions);
        _provider = new MemoryCacheProvider(memoryCache, options);
    }

    [Fact]
    public async Task Get_WithValidKey_ShouldReturnCachedValue()
    {
        const string key = "test-key";
        const string expectedValue = "test-value";
        await _provider.Set(key, expectedValue);

        var result = await _provider.Get<string>(key);

        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task Get_WithNonExistentKey_ShouldReturnNull()
    {
        const string key = "non-existent-key";

        var result = await _provider.Get<string>(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Set_WithNullData_ShouldNotCacheAnything()
    {
        const string key = "test-key";

        await _provider.Set(key, null);

        var isSet = await _provider.IsSet(key);
        isSet.Should().BeFalse();
    }

    [Fact]
    public async Task Set_WithExistingKey_ShouldNotOverwrite()
    {
        const string key = "test-key";
        const string originalValue = "original-value";
        const string newValue = "new-value";

        await _provider.Set(key, originalValue);

        await _provider.Set(key, newValue);

        var cachedValue = await _provider.Get<string>(key);
        cachedValue.Should().Be(originalValue);
    }
}
