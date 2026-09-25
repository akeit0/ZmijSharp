# Local benchmark results

## Current shortest-decomposition comparison

This comparison uses the [shortest-decomposition benchmark](../benchmarks/ZmijSharp.Benchmarks/ShortestDecompositionComparisonBenchmarks.cs) at [revision `5033b1f`](https://github.com/akeit0/ZmijSharp/tree/5033b1f). The [input generator](../benchmarks/ZmijSharp.Benchmarks/ComponentBenchmarks.cs) supplies finite, nonzero `double` values. The benchmark measures canonical `(significand, exponent)` production; digit writing and text presentation are excluded. The comparison port specializes [#131068 at `56ff8516`](../src/UnroundedScaling.Comparison/SOURCE.md) for `double`; these are standalone assemblies, not matched CoreLib builds.

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 `DefaultJob`. Each corpus contains 10,000 deterministic values. A [separate full-cache assembly](../benchmarks/ZmijSharp.Full/ZmijSharp.Full.csproj) and `extern alias` let all three producers run from one benchmark build and command; BenchmarkDotNet still launches each case in its own process. Setup checks equal canonical tuples. Values are **mean (standard deviation)** in ns/value from 14–21 measured iterations after warmup; lower is better. No managed allocations were reported.

| Corpus | Żmij compact | Żmij full | #131068 local port |
|---|---:|---:|---:|
| Simple values | 18.354 (0.104) | 15.215 (0.047) | 15.160 (0.143) |
| Varied long significands | 8.049 (0.033) | 5.613 (0.034) | 6.817 (0.093) |
| Random raw IEEE bits | 10.055 (0.113) | 6.164 (0.145) | 8.613 (0.131) |

Compact Żmij was slower than the local port on these corpora. Full-cache Żmij was faster on varied long significands and random bits and close on simple values. The full cache adds 9,058 source-data bytes and 8,192 B to the minimal DLL versus compact. The [default-job session](benchmark-sessions/shortest-decomposition-default.md) has the reproduction command and links the [earlier ShortRun](benchmark-sessions/same-run-cache-profiles-short.md); [size and assembly](size-and-assembly.md) keeps data, DLL, and native-code counts separate.

The same-source size measurements were refreshed on September 26, 2026. Cached-power and digit-helper rows count raw source constants; the DLL row is the length of the identical `Shortest.Core.dll` project built three ways.

| Size measure | Żmij compact | Żmij full | #131068 local port |
|---|---:|---:|---:|
| Cached-power constants | 830 B | 9,888 B | 11,136 B |
| Listed digit-helper constants | 360 B | 360 B | 432 B |
| **Listed source data** | **1,190 B** | **10,248 B** | **11,568 B** |
| **Minimal Release DLL** | **12,288 B** | **20,480 B** | **19,968 B** |

The #131068 helper constants are existing CoreLib data included in the local port; these counts are not incremental CoreLib image sizes. The separate benchmark assembly used to run both Żmij profiles is excluded from this identical-project DLL comparison.

These measurements isolate local decomposition cost. They do not establish complete formatting speed or predict a CoreLib result. Earlier standalone `Span<char>` comparisons used different digit-staging paths, and their historical long-significand corpus repeated one value due to a generator error. Their timing rows are omitted from this current summary.

## Other local experiments

The standalone direct UTF-8 emitter wrote significand digits into the final destination. On the same raw-bit `double` corpus, it measured 40.91 ns/value against 40.85 ns/value for the buffered emitter in one DefaultJob session (15 measured iterations, zero allocations). It was removed because this run showed no benefit.

The temporary `BigInteger` counted-precision path measured 1,904 ns and 1,125 B per call on `G5`, versus 58 ns and no allocation for runtime `G5`. Explicit `G` precision now delegates to the runtime; a follow-up ShortRun measured 57.53 ns for that fallback versus 57.40 ns for direct runtime `G5`, both allocation-free. These experiments concern removed or fallback paths, not the shortest-decomposition comparison above.

## Consumer benchmark limit

An earlier JSON experiment compared `Utf8JsonWriter` with a hand-written array writer using Żmij. It changed writer and buffer strategy along with conversion, so its timing cannot attribute a gain to the converter. A useful runtime comparison needs the same `System.Text.Json` consumer on matched baseline and candidate CoreLib builds.
