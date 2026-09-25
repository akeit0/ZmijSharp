# Local benchmark results

These are measurements of standalone assemblies on one Windows x64 machine, not matched `dotnet/runtime` builds. The workload session below ran on 2026-09-25; subsequent edits before the initial public revision changed only documentation and a source attribution comment. The local unrounded-scaling comparison assembly adapts a [#131068 source snapshot at `50ef2d06`](../src/UnroundedScaling.Comparison/SOURCE.md); the PR head had advanced to `56ff8516` when this report was updated. Do not quote these rows as a measured win over the current PR or as a projected CoreLib result.

## Environment and method

- Windows 11 x64 (build `10.0.26200.9457`), .NET SDK `11.0.100-rc.1.26425.128`, installed runtime reported by BenchmarkDotNet as `.NET 11.0.0 (11.0.26.42628)`, X64 RyuJIT AVX2. BenchmarkDotNet reported the processor as unknown.
- BenchmarkDotNet 0.14.0, `--job Short`: one launch, three warmup and three measured iterations. `OperationsPerInvoke = 10_000` normalizes each batch to one value.
- Each table below was measured in one session on the same deterministic 10,000-value corpus. Values are raw IEEE patterns for `Random`, cycling common values for `Simple` and `JsonLike`, long-significand cases, or extremes. The source is [`WorkloadBenchmarks.cs`](../benchmarks/ZmijSharp.Benchmarks/WorkloadBenchmarks.cs).
- Complete `TryFormat` rows format into preallocated `Span<char>` buffers. The two significant-digit rows only produce digits and scale. They are diagnostics and must not share a speed ratio with complete formatting.
- Allocations measured zero for every row in the workload session. The short job is directional; near-parity results need repeated, controlled runs before a performance claim.

Reproduce the workload session:

```bash
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*WorkloadBenchmarks*"
```

## Complete shortest `double` formatting

All values and methods in each row use the same workload. Ratio is Zmij time divided by runtime time. The comparison assembly uses the same local presentation writer as Zmij, which helps isolate the effect of their digit producers but is not CoreLib's current writer.

| Corpus | Runtime `TryFormat` | Zmij `TryFormat` | Ratio | Snapshot unrounded scaling `TryFormat` |
|---|---:|---:|---:|---:|
| Simple | 33.68 ns | 27.97 ns | 0.83 | 39.82 ns |
| JsonLike | 31.02 ns | 30.22 ns | 0.97 | 39.53 ns |
| LongSignificand | 65.92 ns | 24.51 ns | 0.37 | 44.81 ns |
| Random | 90.41 ns | 45.38 ns | 0.50 | 63.32 ns |
| Extreme | 50.03 ns | 29.23 ns | 0.58 | 39.32 ns |

`JsonLike` is effectively parity at this level of measurement. The raw-bit and long-significand distributions produce the largest local gaps; they do not represent an average application's value mix. The complete Zmij path differs from CoreLib in parsing and presentation as well as conversion, so these ratios do not isolate the producer's contribution to a runtime patch.

## Digits and scale only

These methods are directly comparable to each other as local producer adapters; they do not include presentation. The snapshot unrounded-scaling adapter is `double` only and rejects zero/non-finite inputs, while Zmij handles zero in its producer. The raw-bit corpus contains very few such values. A prior 2,000,000-pattern audit found identical normalized digits and scale from both adapters on its sampled finite nonzero cases.

| Corpus | Zmij | Snapshot unrounded scaling |
|---|---:|---:|
| Simple | 20.16 ns | 25.21 ns |
| JsonLike | 21.70 ns | 28.03 ns |
| LongSignificand | 17.48 ns | 30.96 ns |
| Random | 24.70 ns | 34.59 ns |
| Extreme | 19.09 ns | 24.64 ns |

## Optimization experiments

The standalone direct UTF-8 emitter tried here wrote significand digits into the final destination. It passed the sampled verifier but did not beat the existing buffered emitter on the same raw-bit `double` corpus: `PathSplitBenchmarks.Zmij` measured **40.85 ns**, and `Zmij_Direct` measured **40.91 ns** in one DefaultJob session (15 measured iterations; zero allocations). The direct variant was removed. This experiment does not rule out a differently structured CoreLib direct writer.

The temporary `BigInteger` counted-precision path was a poor fit for this library. In one ShortRun on `G5`, direct runtime `TryFormat` measured **57.96 ns, 0 B**, while the local counted path measured **1,903.94 ns, 1,125 B**. Explicit `G` precision now delegates to the runtime; a follow-up ShortRun measured **57.40 ns** for direct runtime `G5` and **57.53 ns** for the Zmij fallback, both allocation-free. These before/after sessions are separate and are evidence for removing the counted experiment, not a precise speedup ratio.

## Size

The [size and assembly record](size-and-assembly.md) uses one minimal `double` shortest-producer project for both algorithms, excluding the standalone formatter. Its Release DLLs are 11,776 B for compact Zmij and 18,944 B for the older unrounded-scaling snapshot. It separates these PE lengths from counted source data and x64 JIT code. [`verify_compact_cache.py`](../tools/verify_compact_cache.py) re-derives all 618 compact entries with exact integers.

## Consumer benchmark limit

The earlier JSON experiment compared `Utf8JsonWriter` with a hand-written array writer using Zmij. It changed writer and buffer strategy as well as conversion, so its timing cannot attribute a gain to the converter. A useful runtime comparison needs the same `System.Text.Json` consumer on matched baseline and candidate CoreLib builds.
