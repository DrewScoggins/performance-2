# .NET 11 ASP.NET Performance Investigation Context

## Purpose

This document is a handoff for continuing an investigation into .NET 11 ASP.NET
performance regressions relative to .NET 10. It records the data scope,
methodology, controlled rerun results, attribution calculations, local artifact
locations, and recommended next steps.

The analysis snapshot covers April 1 through August 12, 2026. The controlled
Gold Linux reruns were performed on August 12, 2026.

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
2. Root-cause the April runtime range first. The same product boundary
   reproduced across Fortunes, JSON, and Plaintext Minimal APIs, so one runtime
   change may explain all three.
3. Bisect or inspect the June Plaintext range
   `d8addc1562ad98c7cab19ed654e9caabd1112d87` to
   `ff45a39124929c3f7496f16420d2f0f0265252c4`.
4. Bisect or inspect the July Plaintext range
   `d2b524965e50619e8db62ed35943981830e1008d` to
   `43cdb1f62dde1df80d5f9c3532c8cfacbbc40f53`.
5. Bisect or inspect the August Fortunes Platform range
   `7fb8cef14d9ae6bd729b04638f88d00f1ba7eb99` to
   `a331ec1877b0aca43d294b3d3f91af49bf403ee9`.
6. Correlate the RPS regressions with the associated Gold Linux latency
   regressions. The same root causes may explain both KPIs.
7. Apply the same transition detection and controlled-rerun methodology to:
   - Gold Linux memory, latency, startup, and first-request regressions.
   - Gold Windows.
   - Cobalt Azure Linux 3.
   - Cobalt Azure Ubuntu.
8. Produce a final cross-environment report only after rerunning material
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
