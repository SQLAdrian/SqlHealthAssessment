<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D13 — ICheckRunner Interface (Quick Check Abstraction)

**Date:** 2026-04-18
**Decision:** Introduce `ICheckRunner` abstraction to decouple check orchestration from `VulnerabilityAssessmentService`. Enables subset execution with budget enforcement and per-check timeout.

**Interface:**
```csharp
public interface ICheckRunner
{
    Task<CheckRunResult> RunSubsetAsync(IEnumerable<string> checkIds, TimeSpan budget, CancellationToken ct);
    Task<CheckRunResult> RunAllAsync(CancellationToken ct);
}

public class CheckRunResult
{
    public List<CheckResult> Results { get; set; } = new();
    public int CompletedCount { get; set; }
    public int TimedOutCount { get; set; }
    public TimeSpan TotalElapsed { get; set; }
    public bool BudgetExceeded { get; set; }
    public string[] Warnings { get; set; } = Array.Empty<string>();
}
```

**Default Quick Check subset:** If `checkIds` empty, `RunSubsetAsync` calls `SqlQueryRepository.GetByTag("quick")` → ~40 check IDs.

**Concurrency:** SemaphoreSlim `maxDegree = min(Environment.ProcessorCount, 4)`. Each check gets its own linked CancellationToken; overall budget tracked via Stopwatch; at remaining < heuristic threshold, skip remaining checks.

**Per-check timeout:** Read from `queries.json` entry `timeoutSec` (default 8s). Check cancelled if exceeds limit; counted as `TimedOutCount`, omitted from results (warning emitted).

**WAN mode fallback:** If average connection RTT > 50ms (measured from first 5 checks), automatically reduce `maxDegree=2`, increase per-check timeout to 12s, surface banner "Extended Check mode activated (remote server)." User may continue or cancel.

**Budget enforcement:** Global budget = 55s for 60s SLA. When `TotalElapsed >= 55s`, set `BudgetExceeded=true`, cancel all in-flight checks, return partial results with CTA "run Full VA for complete picture."

**Why abstract:** Allows Quick Check, Full VA, and future custom subsets (e.g., "Security only") to share same concurrency/budget/timeout logic.

**Sources:** Opus §A.2 (ICheckRunner needed, budget 55s, DOP=4, timeout 8s), Prompt 1 (Gemma prompt 2 references interface), WORKFILE existing patterns but no formal interface

