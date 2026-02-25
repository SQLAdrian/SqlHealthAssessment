using System;
using System.Threading.Tasks;
using SqlHealthAssessment.Data.Caching;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Data;

public class SqliteCacheStoreTests
{
    [Fact]
    public async Task CacheStore_CreatesDatabase()
    {
        using var store = new liveQueriesCacheStore();
        var size = await store.GetCacheSizeBytes();
        Assert.True(size > 0);
    }

    [Fact]
    public async Task CacheStore_StoresAndRetrievesTimeSeries()
    {
        using var store = new liveQueriesCacheStore();
        var data = new List<TimeSeriesPoint>
        {
            new() { Time = DateTime.Now, Series = "CPU", Value = 50.0 },
            new() { Time = DateTime.Now.AddMinutes(1), Series = "CPU", Value = 60.0 }
        };

        await store.UpsertTimeSeriesAsync("test.query", "localhost", data, DateTime.Now);
        await store.SetLastFetchTimeAsync("test.query", "localhost", DateTime.Now);

        var retrieved = await store.GetTimeSeriesAsync("test.query", "localhost", 
            DateTime.Now.AddHours(-1), DateTime.Now.AddHours(1));

        Assert.Equal(2, retrieved.Count);
        Assert.Equal("CPU", retrieved[0].Series);
    }

    [Fact]
    public async Task CacheStore_StoresAndRetrievesStatValue()
    {
        using var store = new liveQueriesCacheStore();
        var stat = new StatValue { Label = "CPU", Value = 75.5, Unit = "%", Color = "#ff0000" };

        await store.UpsertStatValueAsync("test.stat", "localhost", stat, DateTime.Now);
        var retrieved = await store.GetStatValueAsync("test.stat", "localhost");

        Assert.NotNull(retrieved);
        Assert.Equal(75.5, retrieved.Value);
        Assert.Equal("%", retrieved.Unit);
    }

    [Fact]
    public async Task CacheStore_TracksLastFetchTime()
    {
        using var store = new liveQueriesCacheStore();
        var now = DateTime.Now;

        await store.SetLastFetchTimeAsync("test.fetch", "localhost", now);
        var retrieved = await store.GetLastFetchTimeAsync("test.fetch", "localhost");

        Assert.NotNull(retrieved);
        Assert.True((retrieved.Value - now).TotalSeconds < 1);
    }

    [Fact]
    public async Task CacheStore_EvictsOldData()
    {
        using var store = new liveQueriesCacheStore();
        var oldData = new List<TimeSeriesPoint>
        {
            new() { Time = DateTime.Now.AddHours(-10), Series = "Old", Value = 1.0 }
        };

        await store.UpsertTimeSeriesAsync("test.evict", "localhost", oldData, DateTime.Now.AddHours(-10));
        await store.EvictOlderThanAsync(TimeSpan.FromHours(8));

        var retrieved = await store.GetTimeSeriesAsync("test.evict", "localhost",
            DateTime.Now.AddHours(-12), DateTime.Now);

        Assert.Empty(retrieved);
    }
}
