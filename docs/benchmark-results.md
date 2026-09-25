# Local benchmark results

## Current shortest-decomposition comparison

This comparison uses the caller-side scaling source at [revision `1e600aa`](https://github.com/akeit0/ZmijSharp/tree/1e600aa1ddebcd65bea2fc2b34427b2f389756de). It measures canonical `(significand, exponent)` production for finite, nonzero `double` values. Digit writing and text presentation are excluded. The comparison port specializes [#131068 at `56ff8516`](../src/UnroundedScaling.Comparison/SOURCE.md) for `double`; these are standalone assemblies, not matched CoreLib builds.

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. Each corpus contains 10,000 deterministic values. Each Żmij profile and its UnroundedScaling control ran in the same launch; compact and full were separate launches. Setup checked equal canonical tuples. Means are ns/value from three measured iterations after three warmups; lower is better. No managed allocations were reported.

| Corpus | Żmij compact | UnroundedScaling, compact launch | Żmij full | UnroundedScaling, full launch |
|---|---:|---:|---:|---:|
| Simple values | 19.033 | 15.524 | 15.493 | 15.161 |
| Varied long significands | 8.510 | 6.585 | 6.177 | 6.632 |
| Random raw IEEE bits | 10.915 | 8.934 | 6.760 | 8.764 |

Compact Żmij was slower than the local port on these corpora. In one full-cache launch, Żmij was faster on varied long significands and random bits and close on simple values. The full cache adds 9,058 source-data bytes and 8,192 B to the minimal DLL versus compact. The [session record](benchmark-sessions/current-cache-comparison-short.md) has the input method and reproduction command; [size and assembly](size-and-assembly.md) keeps data, DLL, and native-code counts separate.

These measurements isolate local decomposition cost. They do not establish complete formatting speed or predict a CoreLib result. Earlier standalone `Span<char>` comparisons used different digit-staging paths, and their historical long-significand corpus repeated one value due to a generator error. Their timing rows are omitted from this current summary.

## Other local experiments

The standalone direct UTF-8 emitter wrote significand digits into the final destination. On the same raw-bit `double` corpus, it measured 40.91 ns/value against 40.85 ns/value for the buffered emitter in one DefaultJob session (15 measured iterations, zero allocations). It was removed because this run showed no benefit.

The temporary `BigInteger` counted-precision path measured 1,904 ns and 1,125 B per call on `G5`, versus 58 ns and no allocation for runtime `G5`. Explicit `G` precision now delegates to the runtime; a follow-up ShortRun measured 57.53 ns for that fallback versus 57.40 ns for direct runtime `G5`, both allocation-free. These experiments concern removed or fallback paths, not the shortest-decomposition comparison above.

## Consumer benchmark limit

An earlier JSON experiment compared `Utf8JsonWriter` with a hand-written array writer using Żmij. It changed writer and buffer strategy along with conversion, so its timing cannot attribute a gain to the converter. A useful runtime comparison needs the same `System.Text.Json` consumer on matched baseline and candidate CoreLib builds.
