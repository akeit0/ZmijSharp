# Shortest-decomposition comparison, BenchmarkDotNet default job

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 `DefaultJob`. The [benchmark class](../../benchmarks/ZmijSharp.Benchmarks/ShortestDecompositionComparisonBenchmarks.cs) at [`5033b1f`](https://github.com/akeit0/ZmijSharp/tree/5033b1f) compares compact Żmij, full-cache Żmij, and the pinned [#131068 local `double` port](../../src/UnroundedScaling.Comparison/SOURCE.md). Its [input generator](../../benchmarks/ZmijSharp.Benchmarks/ComponentBenchmarks.cs) supplies 10,000 deterministic finite nonzero values per corpus. Setup checks equal canonical significands and exponents for every input.

One command ran all nine cases. BenchmarkDotNet used a separate process for each case and selected 14–21 measured iterations after warmup. The full run took 2 minutes 53 seconds. Results are **mean (standard deviation)** in ns/value; lower is better. No managed allocations were reported.

| Corpus | Żmij compact | Żmij full | #131068 local port |
|---|---:|---:|---:|
| Varied long significands | 8.049 (0.033) | 5.613 (0.034) | 6.817 (0.093) |
| Random raw IEEE bits | 10.055 (0.113) | 6.164 (0.145) | 8.613 (0.131) |
| Simple values | 18.354 (0.104) | 15.215 (0.047) | 15.160 (0.143) |

The [earlier ShortRun](same-run-cache-profiles-short.md) had the same ordering on all three corpora. This default-job run confirms that ordering locally, with longer measurements. The comparison stops at canonical decomposition, before digit writing or presentation; the #131068 reference is a local `double` specialization, not a matched CoreLib build.

The size measurements below were refreshed from the same source on September 26, 2026. All minimal DLL rows come from the [same project and public adapter](../size-and-assembly.md#comparable-minimal-dlls), built with each producer. Cached-power bytes are raw source constants, not PE section lengths.

| Size measure | Żmij compact | Żmij full | #131068 local port |
|---|---:|---:|---:|
| Cached-power constants | 830 B | 9,888 B | 11,136 B |
| Minimal Release `Shortest.Core.dll` | 12,288 B | 20,480 B | 19,968 B |

Reproduce from the repository root without a `ZmijCache=Full` override:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --filter "ShortestDecompositionComparisonBenchmarks*"
```
