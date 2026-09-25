# Local benchmark results

These are measurements of standalone assemblies on one Windows x64 machine, not matched `dotnet/runtime` builds. The workload session below ran on 2026-09-25 with a local `double` specialization of [#131068 PR head `56ff8516`](../src/UnroundedScaling.Comparison/SOURCE.md). The Zmij implementation is unchanged from the initial public revision. The local port uses the pinned PR's shortest algorithm and power table and byte-specializes its existing CoreLib digit helpers. These rows compare local implementations; they do not measure the PR's CoreLib integration or project its performance there.

## Environment and method

- Windows 11 x64 (build `10.0.26200.9457`), .NET SDK `11.0.100-rc.1.26425.128`, installed runtime reported by BenchmarkDotNet as `.NET 11.0.0 (11.0.26.42628)`, X64 RyuJIT AVX2. BenchmarkDotNet reported the processor as unknown.
- BenchmarkDotNet 0.14.0, `--job Short`: one launch, three warmup and three measured iterations per session. `OperationsPerInvoke = 10_000` normalizes each batch to one value. Two separate sessions on the same code state are preserved as [session 1](benchmark-sessions/pr-56ff8516-short-1.md) and [session 2](benchmark-sessions/pr-56ff8516-short-2.md). Ranges below show the two session means, not a confidence interval.
- Each session used the same deterministic 10,000-value corpora. Values are raw IEEE patterns for `Random`, cycling common values for `Simple` and `JsonLike`, long-significand cases, or extremes. The source is [`WorkloadBenchmarks.cs`](../benchmarks/ZmijSharp.Benchmarks/WorkloadBenchmarks.cs). The current PR port passed a separate 2,000,000-pattern and binary-exponent-boundary digits, round-trip, and formatted-output check before these runs.
- Complete `TryFormat` rows format into preallocated `Span<char>` buffers. The two significant-digit rows only produce digits and scale. They are diagnostics and must not share a speed ratio with complete formatting.
- Allocations measured zero for every row in both sessions. These two short sessions are directional and did not include an unchanged integer control; near-parity results need a stronger measurement before a performance claim.

Reproduce a workload session:

```bash
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*WorkloadBenchmarks*"
```

## Complete shortest `double` formatting

All values and methods in each row use the same workload. The two local implementations share fixed/scientific presentation code but differ in decimal decomposition and digit writing; the installed runtime uses its own formatter. Values are ns per formatted value.

| Corpus | Runtime `TryFormat` | #131068 local `TryFormat` | Zmij `TryFormat` |
|---|---:|---:|---:|
| Simple | 33.25–34.77 ns | 27.41–30.34 ns | 27.95–30.17 ns |
| JsonLike | 30.36–30.61 ns | 28.08–28.68 ns | 29.93–30.10 ns |
| LongSignificand | 64.91–66.31 ns | 33.57–39.79 ns | 24.40–24.68 ns |
| Random | 88.58–89.46 ns | 49.82–51.38 ns | 44.72–45.48 ns |
| Extreme | 49.03–49.51 ns | 29.83–30.89 ns | 29.08–29.18 ns |

The pinned #131068 port led on `JsonLike` in both sessions; Zmij led on `LongSignificand` and `Random` in both. `Simple` changed order between sessions, and `Extreme` had a small Zmij lead. The raw-bit and long-significand distributions do not represent an average application's value mix. These results cannot rank the two algorithms in CoreLib.

## Digits and scale only

These methods produce digits and scale without complete presentation. The pinned #131068 port is `double` only and skips zero/non-finite inputs, while Zmij handles zero in its producer; `Simple` and `JsonLike` include zero, so those rows do not exercise identical input work. The raw-bit corpus contains very few such values. A fresh 2,000,000-pattern audit found identical normalized digits and scale from both adapters on its sampled finite nonzero cases.

| Corpus | Zmij | #131068 local port |
|---|---:|---:|
| Simple | 20.11–20.17 ns | 15.47–15.92 ns |
| JsonLike | 21.82–21.86 ns | 16.42–16.73 ns |
| LongSignificand | 17.41–17.57 ns | 18.46–18.96 ns |
| Random | 24.36–24.44 ns | 21.45–23.05 ns |
| Extreme | 19.16–19.17 ns | 15.95–16.48 ns |

## Optimization experiments

The standalone direct UTF-8 emitter tried here wrote significand digits into the final destination. It passed the sampled verifier but did not beat the existing buffered emitter on the same raw-bit `double` corpus: `PathSplitBenchmarks.Zmij` measured **40.85 ns**, and `Zmij_Direct` measured **40.91 ns** in one DefaultJob session (15 measured iterations; zero allocations). The direct variant was removed. This experiment does not rule out a differently structured CoreLib direct writer.

The temporary `BigInteger` counted-precision path was a poor fit for this library. In one ShortRun on `G5`, direct runtime `TryFormat` measured **57.96 ns, 0 B**, while the local counted path measured **1,903.94 ns, 1,125 B**. Explicit `G` precision now delegates to the runtime; a follow-up ShortRun measured **57.40 ns** for direct runtime `G5` and **57.53 ns** for the Zmij fallback, both allocation-free. These before/after sessions are separate and are evidence for removing the counted experiment, not a precise speedup ratio.

## Size

The [size and assembly record](size-and-assembly.md) uses one minimal `double` shortest-producer project for both algorithms, excluding the standalone formatter. Its Release DLLs are 11,776 B for compact Zmij and 19,968 B for the pinned #131068 local port. It separates these PE lengths from counted source data and x64 JIT code. [`verify_compact_cache.py`](../tools/verify_compact_cache.py) re-derives all 618 compact entries with exact integers.

## Consumer benchmark limit

The earlier JSON experiment compared `Utf8JsonWriter` with a hand-written array writer using Zmij. It changed writer and buffer strategy as well as conversion, so its timing cannot attribute a gain to the converter. A useful runtime comparison needs the same `System.Text.Json` consumer on matched baseline and candidate CoreLib builds.
