# .NET 11 Gold Linux RPS Regressions: Root Causes and Proposed Fixes

Four material Gold Linux RPS cliffs were reproduced on current hardware and
isolated with alternating parent/candidate runs. Each comparison used the same
hardware, benchmark source, and product base; only the candidate payload or
assembly changed.

## Measured impact

| Boundary | Validated change | Workload | RPS impact | Mean latency | Primary signature |
| --- | --- | --- | ---: | ---: | --- |
| Apr 30 | ASP.NET [#66200](https://github.com/dotnet/aspnetcore/pull/66200): enable runtime async for SharedFx libraries | Plaintext Minimal | 8.05M -> 7.34M (**-8.90%**) | 0.379 -> 0.441 ms (**+16.5%**) | Allocation rose from 0.40 to 31.31 bytes/request |
| Jun 24 | ASP.NET [#67082](https://github.com/dotnet/aspnetcore/pull/67082): record CSRF middleware execution in `HttpContext.Items` | Plaintext Minimal | 7.68M -> 6.42M (**-16.41%**) | 0.396 -> 0.506 ms (**+27.8%**) | Allocation rose from 0.46 to 264.52 bytes/request |
| Jul 29 | runtime [#130884](https://github.com/dotnet/runtime/pull/130884): change Pipelines locking, pooling, and continuation scheduling | Plaintext Minimal | 7.92M -> 7.39M (**-6.68%**) | 0.379 -> 0.458 ms (**+20.8%**) | ThreadPool completed items +32%; queue length +39% |
| Aug 11 | runtime [#131177](https://github.com/dotnet/runtime/pull/131177): batch Linux socket events into ThreadPool work-item trees | Fortunes Platform | 685.9K -> 566.5K (**-17.41%**) | 0.909 -> 1.094 ms (**+20.4%**) | ThreadPool items +102%; contention +49%; CPU -12% |

The isolated changes explain 108%, 87%, 74%, and 108% of their respective
controlled cliffs on a logarithmic scale.

## Proposed fixes

### 1. April: runtime async in the ASP.NET Shared Framework

**Cause:** Enabling runtime async broadly introduced approximately 31 bytes of
allocation per Plaintext request and reduced completed ThreadPool work.

**Immediate mitigation:** Disable runtime async for the affected SharedFx
libraries or hot methods using `UseRuntimeAsync=false` or targeted
`RuntimeAsyncMethodGeneration(false)` annotations.

**Long-term fix:** Re-enable incrementally after the runtime-async lowering and
task/value-task adaptation paths are allocation-neutral and compatible across
the runtime versions used to build and execute the shared framework.

### 2. June: unconditional `HttpContext.Items` materialization

**Cause:** `CsrfProtectionMiddleware` writes a sentinel to
`HttpContext.Items` for every routed endpoint. This eagerly creates the items
dictionary even when the endpoint does not consume a form or require an
antiforgery result.

**Proposed fix:** Remove the unconditional `Items` write. Record middleware
execution through an existing or dedicated feature, or set the marker only
when CSRF/antiforgery metadata or form consumption requires it. Preserve the
invalid-result behavior without allocating storage on unrelated requests.

### 3. July: Pipelines continuation scheduling

**Cause:** The Pipelines change moved continuations toward local ThreadPool
queues. On Gold Linux this increased queued and completed work while reducing
throughput.

**Proposed fix:** Split the PR's scheduling change from its lock and segment
pooling improvements. First restore the prior/global continuation enqueue
behavior, then benchmark the contention and pooling changes independently.
If local queues remain beneficial elsewhere, gate the choice by workload or
scheduler state rather than applying it unconditionally.

### 4. August: Linux socket-event batching

**Cause:** Socket events became visible batched ThreadPool work items. The new
tree dispatch doubled completed work, increased contention, and left CPU
underutilized for the database-backed Fortunes workload.

**Proposed fix:** Restore the prior Linux socket subdispatch path as the first
control. If batching is retained, cap batch size and fan-out, avoid excessive
local-queue chaining, or use an adaptive path for workloads with external
async waits such as database I/O.

## Fix acceptance criteria

For each change, rerun parent/fix/fix/parent with five iterations per batch,
discard iteration one, and require recovery in both RPS and mean latency
without introducing regressions in the other affected Gold Linux workloads.
Then repeat the successful fixes on Gold Windows, Cobalt Azure Linux 3, and
Cobalt Azure Ubuntu before treating them as cross-environment solutions.
