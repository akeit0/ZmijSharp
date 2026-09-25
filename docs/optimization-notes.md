# Digit-stage and x64 JIT experiment

This experiment compares matching `ulong` digit-counting and ASCII digit-writing work in Zmij and the pinned #131068 local port. It also reduces duplicated normalization code in Zmij's `float` and `double` entry points. It does not replace the [complete formatting measurements](benchmark-results.md), which used repository revision `b051752a56215349ba47ead4aa64dd6b9a154102`.

## Reference and decision

The pinned [original Żmij `zmij.cc`](https://github.com/vitaut/zmij/blob/d1682cb47e67474319ed146d3ca2c0e1a70f9429/zmij.cc) converts integer chunks to BCD using base-10,000, base-100, and base-10 stages, with SSE/NEON paths where available. This C# implementation uses a scalar 200-byte pair table and a log2-based digit count. The comparison port uses byte-specialized CoreLib `CountDigits` and `UInt64ToDecChars` routines. The port's `StoreDigits` also removes trailing zeros after writing, so its whole method does more than the isolated writer benchmark.

Same-input BenchmarkDotNet 0.14.0 ShortRun on Windows x64, .NET SDK `11.0.100-rc.1.26425.128`; one launch, three warmup and three measured iterations, 10,000 producer-derived significands per corpus. Values are ns per significand, with zero allocations reported:

| Workload | Zmij digit count | CoreLib-port digit count | Zmij pair writer | CoreLib-port pair writer |
|---|---:|---:|---:|---:|
| Simple | 0.5170 | 0.6034 | 2.360 | 2.409 |
| LongSignificand | 0.5066 | 0.6005 | 6.033 | 7.017 |
| Random raw bits | 0.5146 | 0.5993 | 10.652 | 11.780 |

The temporary writer harness checked every timed input byte-for-byte before measurement. These are small, single-session diagnostic differences, and the complete-format paths include other work. The [digit-count report](benchmark-sessions/digit-count-short.md) and [writer report](benchmark-sessions/digit-write-short.md) preserve the raw summary. `DigitCountBenchmarks` remains runnable with `dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DigitCountBenchmarks*"`. The writer wrapper was removed after it increased the isolated Zmij DLL by 512 bytes; the report is historical and the production writer remains inlined in `TryGetSignificantDigits`.

## Generated x64 code

With tiering disabled and RyuJIT `FullOpts`, the `double` `ToDecimal` method was 520 native bytes before and 301 after consolidating its two finite-result branches into one normalization site and skipping normalization of the internal non-finite sentinel. The finite result is unchanged. The standalone public `ToDecimal` API rejects non-finite values before this internal entry point. In a same-session ShortRun, the old and new entry points were within a few percent: LongSignificand 8.363 vs 8.269 ns, Random 11.596 vs 11.390 ns, and Simple 15.227 vs 15.628 ns. This is a code-size improvement without a demonstrated throughput gain; see the [temporary control report](benchmark-sessions/to-decimal-consolidation-short.md).

| Isolated `double` shortest call tree | Before | After | #131068 local port |
|---|---:|---:|---:|
| Entry adapter | 171 B | 171 B | 246 B |
| Binary-to-decimal entry | 520 B | 301 B | 64 B extraction |
| Conversion | 1,097 B | 1,097 B | 708 B |
| Digit writing, including count | 299 B | 299 B | 373 B, including zero trim |
| **Listed JIT bytes** | **2,087 B** | **1,868 B** | **1,391 B** |
| Minimal Release DLL | 11,776 B | 11,776 B | 19,968 B |

The [before](asm/zmij-to-decimal-before.txt) and [after](asm/zmij-to-decimal-after.txt) listings show the removed duplicate `mulx`/divide-by-10 loop. The current [Zmij digit writer](asm/zmij-digit-writer.txt) has one pair-table load and one two-byte store per pair, with a reciprocal-multiply division by 100. The [#131068 `StoreDigits`](asm/unrounded-store-digits.txt) inlines its table-based digit count, pair writer, and trailing-zero scan. These are native instruction bytes from one x64 JIT, not throughput or CoreLib image estimates.

The minimal profiles still produce the same digits-and-scale SHA-256 digest for 249,884 finite nonzero inputs. The 11 unit tests and a 2,000,000-pattern plus exponent-boundary agreement check also passed after the change.
