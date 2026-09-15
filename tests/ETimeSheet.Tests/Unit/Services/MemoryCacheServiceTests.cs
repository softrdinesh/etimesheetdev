using ETimeSheet.Infrastructure.Services;
using ETimeSheet.Shared.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Tests.Unit.Services;

/// <summary>
/// Tests for the cache wrapper, focused on the two behaviours the application
/// actually relies on: prefix invalidation and not caching nulls.
/// </summary>
[Trait("Category", "Unit")]
public class MemoryCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_RunsTheFactoryOnceAndThenServesFromCache()
    {
        var service = BuildService();
        var calls = 0;

        Task<string> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult("value");
        }

        Assert.Equal("value", await service.GetOrCreateAsync("key", Factory));
        Assert.Equal("value", await service.GetOrCreateAsync("key", Factory));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_DoesNotCacheANullResult()
    {
        var service = BuildService();
        var calls = 0;

        Task<string?> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult<string?>(null);
        }

        await service.GetOrCreateAsync("key", Factory);
        await service.GetOrCreateAsync("key", Factory);

        // Caching "nothing" would turn a transient miss into a sticky one.
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_EvictsOnlyTheMatchingKeys()
    {
        var service = BuildService();

        await service.SetAsync("timelogs:u1:summary:a", "one");
        await service.SetAsync("timelogs:u1:summary:b", "two");
        await service.SetAsync("timelogs:u2:summary:a", "three");

        await service.RemoveByPrefixAsync("timelogs:u1:");

        Assert.Null(await service.GetAsync<string>("timelogs:u1:summary:a"));
        Assert.Null(await service.GetAsync<string>("timelogs:u1:summary:b"));
        Assert.Equal("three", await service.GetAsync<string>("timelogs:u2:summary:a"));
    }

    [Fact]
    public async Task RemoveAsync_EvictsASingleKey()
    {
        var service = BuildService();
        await service.SetAsync("key", "value");

        await service.RemoveAsync("key");

        Assert.Null(await service.GetAsync<string>("key"));
    }

    [Fact]
    public async Task WhenDisabled_EveryReadMissesAndTheFactoryAlwaysRuns()
    {
        var service = BuildService(enabled: false);
        var calls = 0;

        await service.SetAsync("key", "value");
        Assert.Null(await service.GetAsync<string>("key"));

        await service.GetOrCreateAsync("key", _ =>
        {
            calls++;
            return Task.FromResult("value");
        });
        await service.GetOrCreateAsync("key", _ =>
        {
            calls++;
            return Task.FromResult("value");
        });

        Assert.Equal(2, calls);
    }

    private static MemoryCacheService BuildService(bool enabled = true)
    {
        var settings = new CacheSettings
        {
            Enabled = enabled,
            DefaultExpirationSeconds = 60,
            SizeLimit = 64
        };

        return new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = settings.SizeLimit }),
            Options.Create(settings),
            NullLogger<MemoryCacheService>.Instance);
    }
}
