/* In the name of God, the Merciful, the Compassionate */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SQLTriage.Data.Scheduling;

namespace SQLTriage.Tests;

public class QueryOrchestratorTests : IAsyncLifetime
{
    private QueryOrchestrator _orchestrator = null!;
    private QueryRegistry _registry = null!;

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orchestrator:GlobalConcurrency"] = "10",
                ["Orchestrator:PerServerConcurrency"] = "10",
                ["Orchestrator:ChannelCapacity"] = "100",
                ["Orchestrator:DefaultTimeoutSeconds"] = "2"
            })
            .Build();

        _registry = new QueryRegistry(NullLogger<QueryRegistry>.Instance, config);
        _orchestrator = new QueryOrchestrator(NullLogger<QueryOrchestrator>.Instance, config, _registry);
        _orchestrator.Start();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _orchestrator.StopAsync();
        _orchestrator.Dispose();
        _registry.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_SimpleWork_CompletesSuccessfully()
    {
        var request = new QueryRequest
        {
            QueryId = "test:simple",
            Work = async ct => await Task.Delay(10, ct)
        };

        var result = await _orchestrator.EnqueueAsync(request, QueryPriority.P0_Dashboard);

        Assert.True(result.Success);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task EnqueueAsync_PriorityOrdering_P0CompletesBeforeP4()
    {
        // Deterministic priority proof — asserts the actual contract (the
        // dispatcher drains higher-priority channels first) by recording the
        // ORDER in which Work delegates execute, with no wall-clock timing.
        //
        // Why a dedicated single-concurrency orchestrator: the class fixture
        // uses GlobalConcurrency=10, so P0 and P4 Work run in parallel and
        // execution order is not observable — priority only governs DEQUEUE
        // order. With concurrency 1, dequeue order == execution order, so the
        // priority guarantee becomes directly assertable.
        //
        // The flaw in every prior version was a dependency on timing:
        //   v1 assumed 10x50ms P4s can't all finish within a ~10ms P0 window
        //       (false under CI scheduling jitter);
        //   v2 parked P4s on a gate, but the orchestrator cancels work after
        //       Orchestrator:DefaultTimeoutSeconds=2 — a slow CI run timed the
        //       parked P4s out, so All(IsCompleted) flipped true intermittently.
        // This version depends on neither concurrency level nor any timeout.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orchestrator:GlobalConcurrency"] = "1",
                ["Orchestrator:PerServerConcurrency"] = "1",
                ["Orchestrator:ChannelCapacity"] = "100",
                ["Orchestrator:DefaultTimeoutSeconds"] = "30"
            })
            .Build();

        using var registry = new QueryRegistry(NullLogger<QueryRegistry>.Instance, config);
        using var orchestrator = new QueryOrchestrator(NullLogger<QueryOrchestrator>.Instance, config, registry);

        // A single in-flight gate task occupies the lone worker so that all
        // P4s and the P0 are guaranteed to be sitting in their channels
        // BEFORE any of them is dispatched — removing the enqueue-vs-dispatch
        // race entirely. The gate is released only once everything is queued.
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();

        orchestrator.Start();

        var gateTask = orchestrator.EnqueueAsync(new QueryRequest
        {
            QueryId = "test:gate",
            Work = async ct => await releaseGate.Task.WaitAsync(ct)
        }, QueryPriority.P0_Dashboard);

        // With the worker occupied by the gate, queue 10 P4s then 1 P0.
        var p4Tasks = Enumerable.Range(0, 10)
            .Select(i => orchestrator.EnqueueAsync(new QueryRequest
            {
                QueryId = $"test:p4:{i}",
                Work = ct => { executionOrder.Enqueue("p4"); return Task.CompletedTask; }
            }, QueryPriority.P4_Prefetch))
            .ToList();

        var p0Task = orchestrator.EnqueueAsync(new QueryRequest
        {
            QueryId = "test:p0",
            Work = ct => { executionOrder.Enqueue("p0"); return Task.CompletedTask; }
        }, QueryPriority.P0_Dashboard);

        // Everything is now queued. Release the worker and let it drain.
        releaseGate.SetResult();
        await Task.WhenAll(p4Tasks.Append(p0Task).Append(gateTask))
            .WaitAsync(TimeSpan.FromSeconds(20));

        // Contract: P0 must have been dispatched (and so executed, since the
        // single worker runs serially) before any P4. The first non-gate
        // entry in the execution log must be "p0".
        var order = executionOrder.ToArray();
        Assert.Equal("p0", order[0]);
        Assert.Equal(10, order.Count(x => x == "p4"));
    }

    [Fact]
    public async Task EnqueueAsync_ExceededTimeout_ReturnsFailure()
    {
        var request = new QueryRequest
        {
            QueryId = "test:timeout",
            Timeout = TimeSpan.FromMilliseconds(50),
            Work = async ct => await Task.Delay(500, ct)
        };

        var result = await _orchestrator.EnqueueAsync(request, QueryPriority.P1_Alert);

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task EnqueueAsync_CancellationToken_RespectsExternalCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new QueryRequest
        {
            QueryId = "test:cancel",
            Work = async ct => await Task.Delay(10, ct)
        };

        var task = _orchestrator.EnqueueAsync(request, QueryPriority.P1_Alert, cts.Token);

        var ex = await Record.ExceptionAsync(async () => await task);
        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsNonNegativeMetrics()
    {
        var health = await _orchestrator.GetHealthAsync();

        Assert.True(health.QueueDepth >= 0);
        Assert.True(health.InFlightCount >= 0);
        Assert.True(health.CompletedLastMinute >= 0);
        Assert.True(health.FailedLastMinute >= 0);
        Assert.True(health.AverageLatencyLastMinute >= TimeSpan.Zero);
    }

    [Fact]
    public async Task GetMetricsAsync_AfterEnqueue_IncrementsTotalEnqueued()
    {
        var before = await _orchestrator.GetMetricsAsync();

        await _orchestrator.EnqueueAsync(new QueryRequest
        {
            QueryId = "test:metrics",
            Work = async ct => await Task.Delay(10, ct)
        }, QueryPriority.P0_Dashboard);

        var after = await _orchestrator.GetMetricsAsync();

        Assert.True(after.TotalEnqueued > before.TotalEnqueued);
    }
}
