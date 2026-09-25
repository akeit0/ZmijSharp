# Current compact shortest-decomposition comparison

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. This measures the caller-side scaling source after `4d5d6d2` against the pinned local `double` specialization of [dotnet/runtime#131068 at `56ff8516`](../../src/UnroundedScaling.Comparison/SOURCE.md). Each benchmark converts one of 10,000 precomputed finite nonzero `double` values to a canonical significand and exponent. Setup checks that both producers return the same tuple for every value. Three measured iterations follow three warmups. Values are ns/value; no managed allocations were reported.

| Input corpus | Zmij compact | #131068 local port |
|---|---:|---:|
| Simple values | 19.033 | 15.524 |
| Varied long significands | 8.510 | 6.585 |
| Random raw IEEE bits | 10.915 | 8.934 |

The local #131068 producer is faster in this one launch. These methods stop at canonical decomposition: they do not write digits or format text, and the port is a `double` specialization rather than a matched CoreLib build. This table updates the older [pre-caller-placement ShortRun](stacked-cache-short.md) for the current compact source. The [caller-placement A/B table](shared-caller-short.md) answers the separate question of how much the source movement changed Zmij itself.

Reproduce the current compact session:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks.ZmijCanonical" "*DecompositionBenchmarks.UnroundedCanonical"
```
