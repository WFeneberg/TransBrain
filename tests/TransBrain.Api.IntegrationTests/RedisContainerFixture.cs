using StackExchange.Redis;
using Testcontainers.Redis;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// One Redis container shared by every test in <see cref="RedisCacheServiceTests"/>. Each test
/// builds its own <see cref="TransBrain.Infrastructure.Persistence.Caching.RedisCacheService"/>
/// against a per-test-method aggregate prefix (a random GUID segment), so sharing one server
/// across tests in the class cannot make them interfere with each other.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public string ConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
