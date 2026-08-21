# .NET 11 ASP.NET Performance Investigation Context

## Purpose

This document is a handoff for continuing an investigation into .NET 11 ASP.NET
performance regressions relative to .NET 10. It records the data scope,
methodology, controlled rerun results, attribution calculations, local artifact
locations, and recommended next steps.

The analysis snapshot covers April 1 through August 12, 2026. The controlled
Gold Linux boundary reruns were performed on August 12, and the root-cause
validation runs were performed on August 18, 2026. Major improvement
boundaries were validated through August 20, 2026.

## Critical guardrails

- Database access must remain strictly read-only.
- Use only `SELECT` statements and connections configured with
  `ApplicationIntent=ReadOnly`.
- Never write to, alter, or create database tables.
- Do not pass Crank `--sql` or `--table` options during controlled reruns.
- Do not commit raw database documents, credentials, access tokens, internal
  agent addresses, or account information to this public repository.
- The raw data and detailed Crank logs remain local-only. Their paths are
  documented below for another Copilot session running on the same machine.

## Executive summary

The broader extraction covered nine tests, five KPIs, and four environments.
The latest complete .NET 11 and .NET 10 baseline records produced 180
test/environment/KPI comparisons:

- 81 material red regressions.
- 29 smaller yellow adverse changes.
- 70 green, improved, or neutral changes.
- Gold Linux contained 14 material red regressions.
- Four Gold Linux RPS tests were materially regressed:
  - Fortunes Minimal APIs: -6.58%.
  - Fortunes Platform: -21.07%.
  - JSON Minimal APIs: -2.09%.
  - Plaintext Minimal APIs: -13.84%.

Nine sustained Gold Linux RPS transitions were identified. Controlled reruns
used five iterations per batch, exact SDK/runtime/ASP.NET versions, and an
identical benchmark source within every product comparison:

- All six product-build comparisons reproduced a regression.
- None of the three May transitions with identical artifacts reproduced the
  historical regression.
- The reproduced product changes are therefore not explained by the historical
  machine setup.
- The May historical drops are environment, harness, or machine-state events,
  not product-build regressions.

Exact parent/candidate validations isolated the four product cliffs:

- April: ASP.NET PR
  [#66200](https://github.com/dotnet/aspnetcore/pull/66200), enabling
  runtime-async for SharedFx libraries.
- June: ASP.NET PR
  [#67082](https://github.com/dotnet/aspnetcore/pull/67082), which eagerly
  materialized `HttpContext.Items` for routed requests.
- July: runtime PR
  [#130884](https://github.com/dotnet/runtime/pull/130884), changing
  `System.IO.Pipelines` continuation scheduling.
- August: runtime PR
  [#131177](https://github.com/dotnet/runtime/pull/131177), changing Linux
  socket-event dispatch.

The major improvement investigation is documented in
[`net11-gold-linux-rps-improvements.md`](net11-gold-linux-rps-improvements.md):

- July 3 Plaintext is fully explained by ASP.NET #67460 and #67488, which
  compound to +22.433% RPS.
- July 5's historical +5.383% Plaintext jump is not durable on current Gold
  Linux. The exact #129474 and #130181 async changes combine to +1.126% RPS
  while reducing allocation by 55.853%.
- July 29's Fortunes gain is primarily runtime #130884. Local ThreadPool queues
  improve Fortunes but sharply regress Plaintext, requiring an adaptive or
  configurable scheduling policy rather than a universal revert.

Across the four affected RPS KPIs, an equal-weight summary gives:

- Current .NET 11 versus .NET 10 difference: -10.8972%.
- Compounded reproduced product-gap effect: -13.6098%.
- Recoveries and other changes offset 2.7126 percentage points.

RPS from different workloads must not be summed. The equal-weight figure is
only a dashboard-style summary in which each of the four KPIs receives equal
weight.

## Investigation scope

### Tests

1. Plaintext Platform
2. JSON Platform
3. Fortunes Platform
4. Updates Platform
5. Multiple Queries Platform
6. Caching Platform
7. Plaintext Minimal APIs
8. JSON Minimal APIs
9. Fortunes Minimal APIs

### Environments

| Environment | Profile used to identify runs |
| --- | --- |
| Intel Gold Linux | `gold-lin-app` |
| Intel Gold Windows | `gold-win-app` |
| Cobalt Azure Linux 3 | `cobalt-cloud-lin-server-azure-linux3-app` |
| Cobalt Azure Ubuntu | `cobalt-cloud-lin-server-app` |

Cobalt Hosted configurations were deliberately excluded. Cobalt Azure trend
and baseline data begins on April 30, 2026; no earlier matching data was
available.

### KPIs

| KPI | Result metric |
| --- | --- |
| RPS | `load/http/rps/mean` |
| Memory | `application/benchmarks/working-set` |
| Latency | `load/http/latency/mean` |
| Startup | `application/benchmarks/start-time` |
| First Request | `load/http/firstrequest` |

## Read-only extraction

The source contains `TrendBenchmarks` and `BaselineBenchmarks`. Both expose:

`Id`, `Excluded`, `DateTimeUtc`, `Session`, `Scenario`, `Description`,
and `Document`.

Important SQL details:

- `DateTimeUtc` is `datetimeoffset`.
- SQL Server `timestamp` is a deprecated synonym for `rowversion`; it is not a
  date/time column.
- Scenario JSON can be read with:
  `JSON_VALUE([Document], '$.properties.scenario')`.
- The hyphenated command-line property requires:
  `JSON_VALUE([Document], '$.properties."command-line"')`.
- Broad JSON aggregation queries timed out. The successful extractor narrows
  by indexed date and relational scenario, reads one month at a time, and
  parses selected JSON documents locally.
- Historical descriptions changed from `Gold Lin` to `gold-lin`. Profile
  matching is more reliable than description matching.

Authentication used an Azure CLI SQL access token assigned to
`System.Data.SqlClient.SqlConnection.AccessToken`. No password or token is
stored in this repository. The local extractor contains the connection details
and enforces `ApplicationIntent=ReadOnly`.

### Extracted dataset

The snapshot starts at `2026-04-01T00:00:00Z` and ends at
`2026-08-12T20:47:28Z`.

| Item | Count |
| --- | ---: |
| Selected trend runs | 6,707 |
| Selected baseline records | 31,310 |
| Total selected records | 38,017 |
| Complete application/load runs | 29,357 |
| Incomplete runs retained | 8,660 |
| Runs containing all five KPIs | 29,347 |
| Raw document lines including header | 38,018 |
| Missing test/environment combinations | 0 |

Incomplete baseline rows frequently contain only the database job. They were
retained and flagged rather than discarded. Most analysis should filter
`CompleteResult == True`.

Minimal API names differ between trend and baseline records:

| Trend name | Baseline name |
| --- | --- |
| `PlaintextMinimalApis` | `Plaintext` |
| `JsonMinimalApis` | `Json` |
| `FortunesMinimalApis` | `Fortunes` |

## Latest Gold Linux material regressions

The following 14 Gold Linux KPIs were classified as red against the latest
.NET 10 baseline:

| Test | KPI | .NET 10 | .NET 11 | Change |
| --- | --- | ---: | ---: | ---: |
| Caching Platform | Latency | 0.29140 | 0.31318 | +7.47% |
| Caching Platform | Memory | 171 | 206 | +20.47% |
| Fortunes Minimal APIs | Latency | 0.52859 | 0.56332 | +6.57% |
| Fortunes Minimal APIs | RPS | 526,362.10 | 491,705.37 | -6.58% |
| Fortunes Platform | Latency | 0.74805 | 1.10000 | +47.05% |
| Fortunes Platform | RPS | 719,476.69 | 567,854.22 | -21.07% |
| JSON Minimal APIs | Latency | 0.16426 | 0.17866 | +8.77% |
| JSON Minimal APIs | RPS | 1,616,998.13 | 1,583,224.96 | -2.09% |
| Plaintext Minimal APIs | Latency | 0.35564 | 0.53885 | +51.52% |
| Plaintext Minimal APIs | RPS | 8,031,517.75 | 6,919,785.79 | -13.84% |
| Plaintext Minimal APIs | Startup | 209 | 226 | +8.13% |
| Plaintext Platform | First Request | 93 | 108 | +16.13% |
| Updates Platform | Memory | 264 | 282 | +6.82% |
| Updates Platform | Startup | 7,775 | 8,740 | +12.41% |

The red/yellow thresholds were inferred from dashboard behavior and were not
explicitly confirmed:

- RPS is red when it declines by at least 2%.
- Other KPIs are red when they increase adversely by at least 5%.

## Gold Linux RPS transition detection

The trend analysis filtered:

- `SourceTable == TrendBenchmarks`
- `EnvironmentKey == gold-lin`
- `CompleteResult == True`

Five-run rolling medians were used to find sustained changes of approximately
5% or greater. Nine transitions were retained:

| Date | Test | Before RPS | After RPS | Historical sustained change | Initial classification |
| --- | --- | ---: | ---: | ---: | --- |
| Apr 30 | Fortunes Minimal APIs | 545,565 | 523,801 | -5.81% | Product candidate |
| Apr 30 | JSON Minimal APIs | 1,697,679 | 1,565,618 | -5.71% | Product and source mixed |
| Apr 30 | Plaintext Minimal APIs | 8,506,235 | 7,738,196 | -9.92% | Product and source mixed |
| May 20 | JSON Minimal APIs | 1,634,996 | 1,509,504 | -6.39% | Identical artifacts |
| May 21 | Fortunes Minimal APIs | 523,446 | 496,393 | -5.53% | Identical artifacts |
| May 21 | Fortunes Platform | 647,409 | 603,575 | -7.18% | Identical artifacts |
| Jun 24 | Plaintext Minimal APIs | 7,467,986 | 6,235,982 | -15.99% | Product candidate |
| Jul 29 | Plaintext Minimal APIs | 7,823,289 | 7,171,517 | -9.57% | Product candidate |
| Aug 11 | Fortunes Platform | 682,102 | 549,778 | -18.36% | Product candidate |

Do not add the historical percentages. Recoveries occurred between transitions.
Product changes must be compounded, and an explicit recovery/other-effects
remainder is required to reconcile them with the current .NET 10 versus .NET
11 difference.

## Controlled rerun matrix

Crank commit pinning requires `main#<commit>`. Supplying a raw commit causes
Crank to attempt `git clone -b <commit>` and fail.

All runs used:

- Five iterations.
- The Gold Linux application and load profiles.
- The Gold database profile for Fortunes.
- `build/azure.profile.yml`.
- `scenarios/steadystate.profile.yml`.
- `build/ci.profile.yml`.
- `--application.framework net11.0`.
- Explicit SDK, runtime, and ASP.NET versions.
- Dependency and counter collection.
- `--load.options.reuseBuild true`.
- Local JSON and log output only.

The first iteration is normally less stable. The primary comparison is the
last-four mean, with the five-iteration median as a confirmation.

### Product and benchmark-source pins

| Boundary | Test(s) | Before SDK/runtime commit | After SDK/runtime commit | Benchmark source pinned on both sides |
| --- | --- | --- | --- | --- |
| Apr 30 | Fortunes, JSON, and Plaintext Minimal APIs | `11.0.100-preview.5.26228.123` / `c59aad41c35dd3c0c50da3bbb04c540ad59eb0e6` | `11.0.100-preview.5.26229.113` / `4c4e7f410fc876590f219fc6022b6c18d6f6a475` | `8be8c1bebb2bb760b633510dffce10495bd80c7c` |
| May 20-21 | JSON Minimal, Fortunes Minimal, and Fortunes Platform | `11.0.100-preview.5.26269.119` / `85afae000275066ce60df20227f736154bf172ab` | Identical artifacts | `a375ca2d94e996a31c71eeddbfeafb663987d69c` |
| Jun 24 | Plaintext Minimal APIs | `11.0.100-preview.6.26322.111` / `d8addc1562ad98c7cab19ed654e9caabd1112d87` | `11.0.100-preview.6.26323.110` / `ff45a39124929c3f7496f16420d2f0f0265252c4` | `42e7dbae5293db39e5aa1cbdccf3bfca56c07204` |
| Jul 29 | Plaintext Minimal APIs | `11.0.100-preview.7.26366.102` / `d2b524965e50619e8db62ed35943981830e1008d` | `11.0.100-rc.1.26378.118` / `43cdb1f62dde1df80d5f9c3532c8cfacbbc40f53` | `25660cbc6197910908842da07c25c7cde8235ae3` |
| Aug 11 | Fortunes Platform | `11.0.100-rc.1.26409.102` / `7fb8cef14d9ae6bd729b04638f88d00f1ba7eb99` | `11.0.100-rc.1.26410.108` / `a331ec1877b0aca43d294b3d3f91af49bf403ee9` | `09ea584cdf617e9dc64286db9805476bb7a98c6c` |

For the historical April JSON and Plaintext transitions, the benchmark source
also changed. The controlled reruns deliberately pinned the post-transition
source on both sides to isolate the product change.

### Controlled results

Eighteen batches and 90 iterations completed. All requested SDK/runtime/source
pins matched the actual output, and no bad responses or socket errors were
reported.

| Boundary | Test | Historical sustained change | Controlled last-four change | Historical magnitude reproduced | Conclusion |
| --- | --- | ---: | ---: | ---: | --- |
| Apr 30 | Fortunes Minimal APIs | -5.81% | -4.773% | 82.1% | Reproduced |
| Apr 30 | JSON Minimal APIs | -5.71% | -3.120% | 54.6% | Reproduced, smaller |
| Apr 30 | Plaintext Minimal APIs | -9.92% | -8.263% | 83.3% | Reproduced |
| May 20 | JSON Minimal APIs | -6.39% | +0.470% | 0% | Not reproduced; identical artifacts |
| May 21 | Fortunes Minimal APIs | -5.53% | +2.302% | 0% | Not reproduced; opposite direction |
| May 21 | Fortunes Platform | -7.18% | -1.378% | 0% | Not materially reproduced |
| Jun 24 | Plaintext Minimal APIs | -15.99% | -18.595% | 116.3% | Reproduced |
| Jul 29 | Plaintext Minimal APIs | -9.57% | -7.903% | 82.6% | Reproduced |
| Aug 11 | Fortunes Platform | -18.36% | -15.323% | 83.5% | Reproduced |

Last-four coefficients of variation ranged from approximately 0.03% to 0.81%.
The May Fortunes Minimal control moved +2.30% between two identical-artifact
batches, showing that present-day run state can cause a roughly 2% shift, but
it did not reproduce the historical -5.53% regression.

## Root-cause validation

The component matrix first separated runtime and ASP.NET payload effects. Exact
parent/candidate assemblies or complete shared-framework payloads were then
overlaid onto otherwise identical builds and run in
parent/candidate/candidate/parent order.

| Cliff | Validated change | Exact RPS effect | t statistic | Controlled cliff explained |
| --- | --- | ---: | ---: | ---: |
| Apr 30 Plaintext Minimal | ASP.NET #66200 | -8.8962% | -86.5563 | 108.03% |
| Jun 24 Plaintext Minimal | ASP.NET #67082 | -16.4077% | -52.7925 | 87.11% |
| Jul 29 Plaintext Minimal | runtime #130884 | -6.6792% | -23.7810 | 74.26% |
| Aug 11 Fortunes Platform | runtime #131177 | -17.4128% | -41.4368 | 108.17% |

### April: SharedFx runtime async

ASP.NET commit `f676e0fce8b54437b3100cdb824d8cd4e43fbfed` enabled
runtime-async compilation for compatible Shared Framework projects. The full
official ASP.NET payload changed from 8,052,680 to 7,336,294 RPS.

Allocation increased from 3.2 MB/s to 229.7 MB/s, or from 0.40 to 31.31 bytes
per request. The candidate payload also requires its compatible runtime; it
fails on the later April runtime because
`System.Runtime.CompilerServices.ExecutionAndSyncBlockStore` is no longer
present.

### June: eager `HttpContext.Items` creation

The initial runtime PR #128384 hypothesis was rejected. The first successful
VMR build containing it ran at 7.34M RPS with normal allocation.

Full ASP.NET payload bisection instead found the exact adjacent ASP.NET commits:

- Parent: `80b29b3f3b3bc5b9237b2949e70d6aefaa4531bd`.
- Candidate: `d54f274f83350e4cef41eb6a4644f4550d212952`.

PR #67082 changed `CsrfProtectionMiddleware` to write a sentinel into
`HttpContext.Items` whenever a routed endpoint exists. That materializes the
items dictionary on the Plaintext hot path even when no form or antiforgery
result is consumed.

Overlaying only `Microsoft.AspNetCore.dll` changed:

- RPS: 7,678,255 to 6,418,432.
- Allocation: 3.6 MB/s to 1.70 GB/s.
- Allocation per request: 0.46 to 264.52 bytes.

ASP.NET PR #67190 and PR #66807 were separately tested through exact Kestrel
Core overlays and did not reproduce the cliff.

### July: Pipelines continuation scheduling

Runtime commit `2c87bd2b64912ec925d9c419c35fe82d5086b13f` changed
`System.IO.Pipelines` locking, pooling, and continuation scheduling.
`System.IO.Pipelines.dll` alone reduced RPS from 7,920,852 to 7,391,802.
ThreadPool completed items increased 32.2%, and queue length increased 39.4%.

### August: Linux socket dispatch

Runtime commit `7da460b99b9c7e76b3060ef6da6687a008263ab9` moved Linux socket
events into batched ThreadPool work-item trees. The Linux
`System.Net.Sockets.dll` alone reduced RPS from 685,914 to 566,477.
ThreadPool completed items increased 101.9%, lock contention increased 49.5%,
and CPU fell 12.1%.

## Attribution to the current .NET 10 versus .NET 11 gap

The latest paired baselines used:

- .NET 10: `10.0.11+e2f47b0110ed`
- .NET 11: `11.0.0-rc.1.26411.119+7cdb21744590`

Attribution normalizes each .NET 10 result to an index of 100, applies
reproduced product gaps chronologically and multiplicatively, assigns zero
product contribution to the May identical-artifact controls, and then adds a
recovery/other-effects remainder so the result equals the current .NET 11
index.

| Test | Current .NET 10 to .NET 11 gap | Compounded reproduced product gaps | Product gaps' share of net gap | Residual |
| --- | ---: | ---: | ---: | --- |
| Fortunes Minimal APIs | -6.5842% | -4.7730% | 72.5% | 1.8112 points of additional unattributed regression |
| Fortunes Platform | -21.0740% | -15.3230% | 72.7% | 5.7510 points of additional unattributed regression |
| JSON Minimal APIs | -2.0886% | -3.1200% | 149.4% | 1.0314 points of recovery |
| Plaintext Minimal APIs | -13.8421% | -31.2233% | 225.6% | 17.3812 points of recovery |

Plaintext Minimal API attribution:

- April gap: 59.7% of the current net gap.
- June gap: 123.2%.
- July gap: 42.6%.
- Combined reproduced gaps: 225.6%.
- Subsequent recovery/other effects: -125.6%.

The percentages exceed 100% because later improvements offset earlier
regressions.

With equal weighting across the four affected RPS KPIs, each reproduced gap
accounts for the following portion of the overall -10.8972% difference:

| Gap | Share of equal-weight overall difference |
| --- | ---: |
| Apr Fortunes Minimal APIs | 11.0% |
| Apr JSON Minimal APIs | 7.2% |
| Apr Plaintext Minimal APIs | 19.0% |
| Jun Plaintext Minimal APIs | 39.1% |
| Jul Plaintext Minimal APIs | 13.5% |
| Aug Fortunes Platform | 35.2% |
| All May gaps | 0% |
| Recoveries and other effects | -24.9% |

## Earlier Plaintext Platform investigation

Before expanding to all tests and KPIs, Plaintext Platform was investigated
around historical April, May, and August changes.

- An April product difference reproduced at approximately 5.6%.
- The selected May and August historical boundaries had identical product and
  benchmark artifacts and did not materially reproduce.
- A controlled May 5 product versus current product run, using the same
  benchmark source, measured:
  - May product: 19.918 million RPS.
  - Current product: 21.540 million RPS.
  - Current product: 8.15% faster.
- Benchmark-source differences accounted for only approximately 0.3%.
- A pre-April product versus current product run, using today's identical
  benchmark source, measured:
  - Pre-April product: 20.930 million RPS.
  - Current product: 21.540 million RPS.
  - Current product: 2.92% faster.

This demonstrated that raw dashboard level changes can contain substantial
environmental baseline drift and that historical drops must be retested with
controlled artifacts.

## Local-only artifact inventory

Another Copilot session on this machine can reconstruct the absolute root with:

```powershell
$analysisRoot = Join-Path $env:USERPROFILE `
    '.copilot\session-state\51486ad1-b56e-486f-8d96-25dbdbd289c6\files\benchmark-analysis-data'
```

### Extraction and normalized data

| Relative path | Purpose |
| --- | --- |
| `extract-benchmark-data.ps1` | Reproducible monthly read-only extractor |
| `export\manifest.json` | Scope, counts, dates, validation, and file catalog |
| `export\kpi-runs.csv` | Primary one-row-per-run KPI dataset |
| `export\runs.csv` | Detailed run metadata and completeness flags |
| `export\metrics.csv.gz` | All selected result metrics and runtime counters |
| `export\dependencies.csv.gz` | Dependency versions and commits |
| `export\documents.tsv.gz` | Exact selected raw JSON documents |
| `export\latest-net11-vs-net10.csv` | Latest baseline comparisons |
| `export\gold-lin-rps-major-transitions.csv` | Curated nine RPS transitions |
| `export\gold-lin-rps-transition-candidates.csv` | All rolling-median candidates |

### Controlled Gold Linux RPS reruns

The rerun root is:

```powershell
$rerunRoot = Join-Path $analysisRoot 'gold-lin-rps-reruns'
$controlledRoot = Join-Path $rerunRoot 'controlled-20260812'
```

| Relative path | Purpose |
| --- | --- |
| `gold-lin-rps-reruns\source-boundaries.csv` | Exact source rows for all boundaries |
| `gold-lin-rps-reruns\run-controlled-reruns.ps1` | Resumable 18-batch rerun matrix |
| `gold-lin-rps-reruns\analyze-controlled-reruns.ps1` | Iteration, batch, and pair analysis |
| `gold-lin-rps-reruns\controlled-20260812\matrix.json` | Executed matrix |
| `gold-lin-rps-reruns\controlled-20260812\status.csv` | All 18 completed batches |
| `gold-lin-rps-reruns\controlled-20260812\iteration-rps.csv` | All 90 iteration values |
| `gold-lin-rps-reruns\controlled-20260812\batch-summary.csv` | Per-batch statistics and pin validation |
| `gold-lin-rps-reruns\controlled-20260812\pair-summary.csv` | Primary before/after results |
| `gold-lin-rps-reruns\controlled-20260812\analysis.json` | Machine-readable complete analysis |
| `gold-lin-rps-reruns\controlled-20260812\net10-net11-gap-attribution.csv` | Detailed waterfall attribution |
| `gold-lin-rps-reruns\controlled-20260812\net10-net11-attribution-summary.csv` | Per-test attribution |
| `gold-lin-rps-reruns\controlled-20260812\net10-net11-attribution-equal-weight-overall.csv` | Equal-weight overall shares |

Each controlled case also has a local JSON result and complete Crank log in the
`controlled-20260812` directory.

### Root-cause validation artifacts

The root-cause directory is:

```powershell
$rootCauseRoot = Join-Path $analysisRoot 'gold-lin-rps-root-cause'
```

| Relative path | Purpose |
| --- | --- |
| `gold-lin-rps-root-cause\candidate-analysis.md` | Current authoritative root-cause report |
| `gold-lin-rps-root-cause\component-splits-20260818\component-split-analysis.md` | Runtime/ASP.NET component attribution |
| `gold-lin-rps-root-cause\candidate-runs-20260818\aspnet-66200-full-overlay\exact-candidate-analysis.md` | Exact April validation |
| `gold-lin-rps-root-cause\candidate-runs-20260818\aspnet-67082-defaultbuilder\exact-candidate-analysis.md` | Exact June validation |
| `gold-lin-rps-root-cause\candidate-runs-20260818\runtime-130884\exact-candidate-analysis.md` | Exact July validation |
| `gold-lin-rps-root-cause\candidate-runs-20260818\runtime-131177-sockets\exact-candidate-analysis.md` | Exact August validation |
| `gold-lin-rps-root-cause\june-full-overlay-probes` | June ASP.NET payload bisection |
| `gold-lin-rps-root-cause\runtime-async-mechanism-20260818` | ReadyToRun-disabled April and June checks |

### Improvement validation artifacts

The improvement directory is:

```powershell
$improvementRoot = Join-Path $analysisRoot 'gold-lin-rps-improvements'
```

| Relative path | Purpose |
| --- | --- |
| `gold-lin-rps-improvements\gold-lin-rps-improvement-findings.md` | Authoritative improvement report |
| `gold-lin-rps-improvements\candidate-runs\aspnet-67460` | Exact July 3 CSRF-gating validation |
| `gold-lin-rps-improvements\candidate-runs\aspnet-67488` | Exact July 3 `HttpContext.Items` validation |
| `gold-lin-rps-improvements\candidate-runs\runtime-129474-crossgen2-combined-analysis.md` | Replicated #129474 result |
| `gold-lin-rps-improvements\july5-component-analysis` | Official July 5 package and ReadyToRun matrix |
| `gold-lin-rps-improvements\july5-exact-async-matrix` | Exact #129474 by #130181 interaction matrix |
| `gold-lin-rps-improvements\candidate-runs\runtime-130884-fortunes` | Exact July 29 Fortunes validation |
| `gold-lin-rps-improvements\pipelines-subset-runs\pipelines-cross-workload-analysis.md` | Plaintext/Fortunes scheduling tradeoff |

### Earlier Plaintext Platform reruns

| Relative path | Purpose |
| --- | --- |
| `gold-lin-reruns\summary.csv` | Initial April/May/August comparison |
| `gold-lin-may5-vs-latest\summary.json` | May 5 versus current comparison |
| `gold-lin-may5-vs-latest\pre-apr30-vs-current-summary.json` | Pre-April versus current comparison |

## Useful continuation commands

Read the primary controlled comparison:

```powershell
Import-Csv (Join-Path $controlledRoot 'pair-summary.csv') |
    Format-Table Event, Test, HistoricalSustainedDropPct,
        LastFourMeanDeltaPct, Conclusion -AutoSize
```

Regenerate analysis from completed local logs:

```powershell
& (Join-Path $rerunRoot 'analyze-controlled-reruns.ps1')
```

Refresh the read-only database snapshot through the current time:

```powershell
& (Join-Path $analysisRoot 'extract-benchmark-data.ps1') `
    -StartUtc '2026-04-01T00:00:00Z' `
    -EndUtc ([DateTimeOffset]::UtcNow)
```

Before refreshing, confirm the Azure CLI is signed in to an identity with
read-only access. Do not modify the extractor to perform writes.

## Recommended next work

1. Refresh the extraction because the checked analysis ends on August 12,
   2026.
2. Validate candidate fixes:
   - Targeted runtime-async opt-outs or fixes for the April SharedFx paths.
   - Avoiding unconditional `HttpContext.Items` materialization in June.
   - Reverting or retuning the July Pipelines local-queue behavior.
   - Reverting or retuning the August Linux socket batching.
3. Prototype an adaptive or configurable Pipelines continuation-placement
   policy and validate both Plaintext and Fortunes.
4. Correlate the RPS regressions with the associated Gold Linux latency
   regressions. The same root causes may explain both KPIs.
5. Apply the same transition detection and controlled-rerun methodology to:
   - Gold Linux memory, latency, startup, and first-request regressions.
   - Gold Windows.
   - Cobalt Azure Linux 3.
   - Cobalt Azure Ubuntu.
5. Produce a final cross-environment report only after rerunning material
   boundaries on current hardware.

## Caveats

- This is a snapshot, not live data.
- Dashboard red/yellow thresholds were inferred.
- Latest-pair comparisons and historical trend runs answer different
  questions; do not substitute one for the other.
- Historical percentages must be compounded, not added.
- A reproduced transition can account for more than 100% of the current net
  gap when later recoveries offset it.
- The attribution residual contains untested product changes, pre-existing
  .NET 10 versus .NET 11 differences, recoveries, benchmark-source changes, and
  any remaining environmental effects.
- The public context deliberately excludes raw documents and infrastructure
  access details. Use the local-only artifacts for exact evidence.
