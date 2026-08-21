# .NET 11 Gold Linux RPS Improvements

Three historical throughput improvements were investigated with controlled
parent/candidate overlays and balanced reruns.

| Boundary | Historical change | Current result |
| --- | ---: | --- |
| July 3 Plaintext Minimal APIs | +21.051% | ASP.NET #67460 and #67488 compound to +22.433% |
| July 5 Plaintext Minimal APIs | +5.383% immediate, +3.53% sustained | Exact async changes combine to +1.126%; historical magnitude is not reproducible |
| July 29 Fortunes Platform | +13.008% | runtime #130884 reproduces +10.966% |

## July 3: avoid unnecessary request work

ASP.NET [#67460](https://github.com/dotnet/aspnetcore/pull/67460) gates
CSRF validation to endpoints that opt in:

- RPS: 6,455,882 to 6,755,846 (**+4.646%**).
- Mean latency: **-4.468%**.

ASP.NET [#67488](https://github.com/dotnet/aspnetcore/pull/67488) avoids
allocating `HttpContext.Items` on ordinary requests:

- RPS: 6,758,626 to 7,904,154 (**+16.949%**).
- Mean latency: **-20.935%**.
- Allocation rate: **-99.546%**.

Applied sequentially, the changes produce **+22.433%**, explaining 105.94% of
the historical boundary on a logarithmic scale.

## July 5: allocation improved, but the RPS cliff was not durable

A balanced rerun of the exact official before/after packages did not reproduce
the historical throughput increase:

- RPS: **-0.809%**, t=-2.0607.
- RPS with ReadyToRun disabled: **-0.347%**, t=-0.5103.
- Allocation rate: **-59.447%**.
- Working set: **-4.467%**.

Runtime [#129474](https://github.com/dotnet/runtime/pull/129474) added 220
ReadyToRun methods to the ASP.NET payload: 216 async variants and 4 resume
variants. Opposite-order repetitions measured +2.1608% and -0.6254%. Their
order-balanced estimate is **+0.7581%**, with a batch-level t statistic of
0.9066. The binary mechanism is proven, but a stable standalone RPS gain is
not.

Runtime [#130181](https://github.com/dotnet/runtime/pull/130181) enables
tail-await optimization in Tier 0:

- Standalone RPS: **+0.7183%**, t=1.9087.
- Allocation rate: **-56.477%**.
- All 133 ASP.NET assemblies were byte-identical when recompiled with exact
  parent/candidate Crossgen2 tools, confirming this is a dynamic Tier 0 effect.

An exact 2x2 matrix combining the #129474 ReadyToRun payloads with the #130181
JITs measured:

- Combined RPS: 7,879,996 to 7,968,717 (**+1.126%**), t=3.0353.
- Symmetric #129474 effect: **+0.512%**.
- Symmetric #130181 effect: **+0.611%**.
- Interaction: **-1.389%**.
- Allocation rate: **-55.853%**.
- Mean latency: **-1.955%**.

Together these changes explain 21.35% of the historical immediate gain, or
32.27% of the sustained gain. The allocation improvement is real and
repeatable, but the historical RPS magnitude was sensitive to machine or run
state.

Exact Crossgen2 checks also eliminated runtime #127746 and #129325 as ASP.NET
ReadyToRun causes: each produced 133 byte-identical parent/candidate
assemblies.

## July 29: continuation placement is workload dependent

Runtime [#130884](https://github.com/dotnet/runtime/pull/130884) reproduced
most of the Fortunes improvement:

- Fortunes RPS: 612,555 to 679,725 (**+10.966%**).
- Fortunes P50 latency: **-16.204%**.
- Historical gain explained: **85.09%**.
- JSON RPS: **+1.791%**, explaining 49.73% of its historical gain.

The Pipelines split identified `preferLocal: true` as the workload
discriminator:

| Variant | Plaintext RPS | Fortunes RPS |
| --- | ---: | ---: |
| Full candidate | -6.592% | +9.754% |
| Candidate without local queues | +7.296% | -0.474% |
| Local queues only | -14.963% | +11.203% |

The lock, buffer-movement, and FIFO segment-pool changes help Plaintext and are
neutral on Fortunes. Local queues improve Fortunes while sharply regressing
Plaintext, so a blanket revert would discard a proven improvement.

## Engineering takeaways

1. Avoid unconditional per-request work and lazy-state materialization on hot
   paths.
2. Treat allocation and throughput as separate acceptance metrics.
3. Keep runtime-async ReadyToRun generation, but do not credit it with the
   historical July 5 RPS magnitude.
4. Make Pipelines continuation placement configurable or adaptive rather than
   universally preferring local queues.
