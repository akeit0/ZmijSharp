# Shortest-conversion component benchmarks

These benchmarks split the local `double` paths at three boundaries: shortest decimal decomposition, digit storage, and ASCII-to-`char` staging. They compare the compact Zmij producer with the [pinned #131068 local port](../src/UnroundedScaling.Comparison/SOURCE.md). The aim is to locate costs inside these standalone implementations; none of the timings is a matched CoreLib measurement.

## Inputs and checks

[`ComponentBenchmarks.cs`](../benchmarks/ZmijSharp.Benchmarks/ComponentBenchmarks.cs) uses 10,000 finite, nonzero values per workload. `Simple` cycles 14 common values, `LongSignificand` varies 52 mantissa bits under a fixed binary exponent, and `Random` draws deterministic IEEE bit patterns, substituting 1.25 for zero and nonfinite encodings. Setup requires at least 9,000 distinct long-significand values. The historical workload reports in [benchmark-results](benchmark-results.md) predate this guard and repeated one value because of a mask error; **these component reports use the corrected corpus**.

Setup checks every Zmij canonical `(significand, exponent)` against the local port's canonical tuple. It also checks the span and pointer buffer variants byte for byte, including the terminator, and verifies their count and scale against Zmij. The timed methods use the same value or precomputed integer sequence within each workload. Buffer benchmarks exclude decimal decomposition; decomposition benchmarks exclude digit writing and presentation.

Windows 11 x64 build `10.0.26200.9457`; .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC runtime, X64 RyuJIT AVX2; BenchmarkDotNet 0.14.0 ShortRun. Each of two sessions has one launch, three warmup iterations, three measured iterations, and 10,000 operations per invocation. Tables below give the two session means in nanoseconds per value. All rows reported zero managed allocations. Raw BenchmarkDotNet summaries: [decomposition session 1](benchmark-sessions/decomposition-components-1.md), [session 2](benchmark-sessions/decomposition-components-2.md), [buffer session 1](benchmark-sessions/buffer-components-1.md), [session 2](benchmark-sessions/buffer-components-2.md).

## Decimal decomposition without digit writing

`ZmijCanonical` calls `ZmijCore.ToDecimal` and takes the canonical significand and decimal exponent. `UnroundedRaw` uses the pinned port's extraction, normalization, and `ShortFloat` but returns its untrimmed integer and exponent. `UnroundedCanonical` adds an **integer** trailing-zero loop to return the same canonical tuple as Zmij. Production `TryRun` instead trims written **bytes** in `StoreDigits`; the canonical helper is an equal-output diagnostic, not production #131068 code. Zmij's entry point also retains zero/nonfinite and sign handling on these finite nonzero inputs.

| Workload | Zmij canonical | #131068 local canonical | #131068 local raw |
|---|---:|---:|---:|
| Simple | 19.45 ns | 14.99–15.19 ns | 4.30–4.31 ns |
| LongSignificand | 8.99–9.05 ns | 6.76–6.81 ns | 6.23–6.35 ns |
| Random | 11.59–11.81 ns | 8.51–9.11 ns | 8.40–8.57 ns |

At this equal tuple boundary, the local #131068 specialization was faster on all three corpora. The raw column has a different output contract. Do not subtract it from the canonical time to estimate trailing-zero cost: separate methods can be inlined and optimized differently. The `Simple` corpus contains powers of ten, which cause repeated divisions in the diagnostic canonical helper; production `StoreDigits` trims characters instead.

## Buffer and staging after scaling

The buffer benchmarks start from the same precomputed #131068 `ShortFloat` integers and exponents. `PointerBuffer` calls the port's `StoreDigits` with an `UnroundedBuffer`, including digit count, byte-pair writing, trailing-zero scan, NUL terminator, and scale. `SpanBuffer` repeats those operations with a bounded `Span<byte>` and `(count, scale)` result. `PointerBufferThenWiden` adds the local comparison formatter's byte-to-`char` copy to a stack buffer. Each checksum consumes count, scale, and the last stored digit.

| Workload | Pointer buffer | Span buffer | Pointer then widen |
|---|---:|---:|---:|
| Simple | 11.69–12.01 ns | 11.34–11.68 ns | 16.27–16.41 ns |
| LongSignificand | 12.05–12.15 ns | 11.99–12.15 ns | 20.53–20.63 ns |
| Random | 15.09–15.33 ns | 15.07–15.28 ns | 26.80–26.96 ns |

The pointer and span rows were very close in both sessions. This gives no evidence that the port's pointer-backed buffer structure is its main cost. The local byte-to-`char` staging shows a larger increase, about 4–12 ns/value in this harness, depending on workload. The [generated x64 wrapper assembly](asm/unrounded-char-wrapper.txt) confirms the comparison formatter's per-digit byte load and 16-bit store. This staging belongs to this repository's local `char` adapter; it is not a measured cost of #131068's CoreLib `NumberBuffer` implementation. These components are independent probes and their means are not additive parts of the complete `TryFormat` time. JIT layout and inlining can also change when a stage is isolated.

Reproduce either probe:

```bash
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks*"
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*BufferStyleBenchmarks*"
```
