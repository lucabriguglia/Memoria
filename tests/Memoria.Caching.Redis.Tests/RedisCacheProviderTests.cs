using FluentAssertions;
using Memoria.Caching.Redis.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Memoria.Caching.Redis.Tests;

public class RedisCacheProviderTests
{
    private readonly IDatabase _database;
    private readonly RedisCacheProvider _provider;

    public RedisCacheProviderTests()
    {
        var connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        var defaultOptions = new RedisCacheOptions
        {
            DefaultCacheTimeInSeconds = 300,
            ConnectionString = "localhost:6379"
        };
        var options = Substitute.For<IOptions<RedisCacheOptions>>();
        options.Value.Returns(defaultOptions);

        connectionMultiplexer
            .GetDatabase(Arg.Any<int>(), Arg.Any<object>())
            .Returns(_database);

        _provider = new RedisCacheProvider(connectionMultiplexer, options);
    }

    [Fact]
    public async Task Get_WithValidKey_ShouldReturnDeserializedValue()
    {
        const string key = "test-key";
        const string expectedValue = "test-value";
        var serializedValue = JsonConvert.SerializeObject(expectedValue);

        _database
            .StringGetAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(new RedisValue(serializedValue));

        var result = await _provider.Get<string>(key);

        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task Get_WithNonExistentKey_ShouldReturnNull()
    {
        const string key = "non-existent-key";

        _database
            .StringGetAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        var result = await _provider.Get<string>(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsSet_WithExistingKey_ShouldReturnTrue()
    {
        const string key = "test-key";

        _database
            .KeyExistsAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(true);

        var result = await _provider.IsSet(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSet_WithNonExistentKey_ShouldReturnFalse()
    {
        const string key = "non-existent-key";

        _database
            .KeyExistsAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(false);

        var result = await _provider.IsSet(key);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_WithExistingKey_ShouldRemoveFromCache()
    {
        const string key = "test-key";

        // Setup: key exists initially, then doesn't exist after removal
        _database
            .KeyExistsAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(true, false);

        _database
            .KeyDeleteAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(true);

        var isSetBefore = await _provider.IsSet(key);
        isSetBefore.Should().BeTrue();

        await _provider.Remove(key);

        var isSetAfter = await _provider.IsSet(key);
        isSetAfter.Should().BeFalse();

        await _database.Received(1).KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k == key),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Remove_WithNonExistentKey_ShouldNotThrow()
    {
        const string key = "non-existent-key";

        _database
            .KeyDeleteAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(false);

        var act = async () => await _provider.Remove(key);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CacheExpiration_ShouldWork()
    {
        const string key = "expiring-key";
        const string value = "expiring-value";
        const int shortCacheTime = 1;
        var serializedValue = JsonConvert.SerializeObject(value);

        _database
            .StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        // First call returns the value, second call (after delay) returns null (expired)
        _database
            .StringGetAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(new RedisValue(serializedValue), RedisValue.Null);

        await _provider.Set(key, value, shortCacheTime);

        var immediateResult = await _provider.Get<string>(key);
        immediateResult.Should().Be(value);

        // Simulate cache expiration by returning null on second call
        var expiredResult = await _provider.Get<string>(key);
        expiredResult.Should().BeNull();
    }

    [Fact]
    public async Task Set_WithComplexObject_ShouldSerializeAndDeserializeCorrectly()
    {
        const string key = "complex-object-key";
        var complexObject = new TestComplexObject
        {
            Id = 123,
            Name = "Test Object",
            CreatedAt = DateTime.UtcNow,
            Tags = new List<string> { "tag1", "tag2", "tag3" }
        };
        var serializedValue = JsonConvert.SerializeObject(complexObject);

        _database
            .StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        _database
            .StringGetAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(new RedisValue(serializedValue));

        await _provider.Set(key, complexObject);

        var cachedObject = await _provider.Get<TestComplexObject>(key);
        cachedObject.Should().NotBeNull();
        cachedObject.Should().BeEquivalentTo(complexObject);
    }

    [Fact]
    public async Task Get_WithComplexObject_ShouldDeserializeCorrectly()
    {
        const string key = "complex-object-key";
        var expectedObject = new TestComplexObject
        {
            Id = 123,
            Name = "Test Object",
            CreatedAt = DateTime.UtcNow,
            Tags = new List<string> { "tag1", "tag2" }
        };
        var serializedValue = JsonConvert.SerializeObject(expectedObject);

        _database
            .StringGetAsync(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>())
            .Returns(new RedisValue(serializedValue));

        var result = await _provider.Get<TestComplexObject>(key);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedObject);
    }

    [Fact]
    public async Task DatabaseProperty_ShouldUseOptionsValues()
    {
        var customOptions = new RedisCacheOptions
        {
            DefaultCacheTimeInSeconds = 600,
            ConnectionString = "custom:6379",
            Db = 5,
            AsyncState = "custom-state"
        };

        var customConnectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        var customDatabase = Substitute.For<IDatabase>();
        var customOptionsWrapper = Substitute.For<IOptions<RedisCacheOptions>>();

        customOptionsWrapper.Value.Returns(customOptions);
        customConnectionMultiplexer
            .GetDatabase(customOptions.Db, customOptions.AsyncState)
            .Returns(customDatabase);

        customDatabase
            .StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        var provider = new RedisCacheProvider(customConnectionMultiplexer, customOptionsWrapper);

        await provider.Set("test", "value");

        customConnectionMultiplexer.Received(1).GetDatabase(
            customOptions.Db,
            customOptions.AsyncState);
    }

    private class TestComplexObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
