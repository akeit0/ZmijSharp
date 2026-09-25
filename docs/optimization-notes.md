# Digit-stage and x64 JIT experiment

This experiment compares matching `ulong` digit-counting and ASCII digit-writing work in Zmij and the pinned #131068 local port. It also reduces duplicated normalization code in Zmij's `float` and `double` entry points. It does not replace the [complete formatting measurements](benchmark-results.md), which used repository revision `b051752a56215349ba47ead4aa64dd6b9a154102`. A later [component benchmark](component-benchmarks.md) isolates decimal decomposition and buffer staging with a corrected varied long-significand corpus.

## Reference and decision

The pinned [original Żmij `zmij.cc`](https://github.com/vitaut/zmij/blob/d1682cb47e67474319ed146d3ca2c0e1a70f9429/zmij.cc) converts integer chunks to BCD using base-10,000, base-100, and base-10 stages, with SSE/NEON paths where available. This C# implementation uses a scalar 200-byte pair table and a log2-based digit count. The comparison port uses byte-specialized CoreLib `CountDigits` and `UInt64ToDecChars` routines. The port's `StoreDigits` also removes trailing zeros after writing, so its whole method does more than the isolated writer benchmark.

The historical `LongSignificand` generator in these sessions repeated **2^53−1** rather than producing varied values. The mask is corrected in the current benchmark sources. Treat that row and the temporary integration timing below as a single-value case. The BCD8 probe itself generates varied 16-digit significands independently.

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

## Scalar BCD8 trial

The retained [BCD8 probe](../benchmarks/ZmijSharp.Benchmarks/Bcd16ProbeBenchmarks.cs) ports the original scalar arithmetic to two eight-digit chunks and checks its 16-byte output against the pair-table writer for 10,000 producer-derived 16-digit significands. In one same-session ShortRun, BCD8 took **3.689 ns/value** versus **5.698 ns/value** for the pair-table writer ([raw report](benchmark-sessions/bcd16-probe-short.md)).

A temporary integration applied BCD8 only to 16-digit Zmij results. It passed the 2,000,000-pattern agreement check, and the digits-only `LongSignificand` row measured 13.64 ns/value, below the earlier pair-table sessions at 17.41–17.64 ns/value. The minimal DLL increased by 512 B to 12,288 B. Complete `TryFormat` in that ShortRun measured **24.37 ns/value** for `LongSignificand` and **44.87 ns/value** for raw-bit `Random`, within the earlier pair-table ranges of 24.40–24.68 and 44.72–45.48 ns/value ([full trial session](benchmark-sessions/bcd16-complete-short.md)). Because the complete path showed no useful gain for the size cost, the production BCD8 branch was removed. The BCD8 probe remains for future architecture-specific experiments.

## Later `ToDecimal` and SIMD trials

The current `DecimalResult` uses an `int` digit with `-1` as the absent-digit marker. This removes the checked byte conversion and decoding from the private 16-byte result. In the minimal-profile Windows x64 .NET 11 RC JIT listing, the `double` entry is **298** native bytes and its conversion method is **1,065** bytes, compared with **301** and **1,097** bytes in the earlier listing above. The minimal Zmij DLL remains **11,776 B**. The 249,884-input minimal-profile digest, 11 unit tests, and 2,000,000 random inputs plus binary-exponent boundaries agree with the pinned #131068 port. One decomposition ShortRun measured 8.673 ns/value for `LongSignificand`, 11.396 for `Random`, and 19.344 for `Simple`; these are within the earlier ranges, so the change is a code-size cleanup rather than a demonstrated throughput improvement.

Other `ToDecimal` changes were measured and removed:

| Trial | Observation | Decision |
|---|---|---|
| Divide trailing decimal zeros in 8/4/2/1 chunks | Minimal DLL grew by 512 B; `Random` decomposition rose to 13.19–13.46 ns/value in two ShortRuns, from 11.59–11.81 ns/value in the earlier two sessions | Reverted |
| Unify regular and irregular scaling branches | Conversion method fell from 1,097 to 761 native bytes, but decomposition rose to 11.595 ns `LongSignificand`, 15.000 ns `Random`, and 25.634 ns `Simple` in one ShortRun | Reverted |
| Select compact-cache normalization with bit masks | Removed a branch, but the conversion method grew from 1,068 to 1,086 native bytes and decomposition rose to 10.557 ns `LongSignificand`, 12.643 ns `Random`, and 20.789 ns `Simple` in one ShortRun | Reverted |
| Replace three cache table bounds checks with one explicit guard and `Unsafe.Add` | Minimal DLL grew by 512 B; conversion method grew from 1,068 to 1,140 native bytes; `Random` decomposition rose to 13.099 ns/value in one ShortRun | Reverted |

The [16-digit BCD probe](../benchmarks/ZmijSharp.Benchmarks/Bcd16ProbeBenchmarks.cs) now also tests an SSE2 ASCII add and 16-byte store against the same pair-writer output. In two ShortRuns, the pair writer took **5.692–5.749 ns**, scalar BCD **3.719–3.732 ns**, and SSE2 BCD **3.619–3.640 ns** per 16-digit value ([session summary](benchmark-sessions/simd-bcd16-short.md)). The SIMD advantage over scalar BCD is about 0.1 ns in this isolated stage. The earlier scalar BCD integration did not improve complete formatting enough to justify its 512 B DLL cost, so the SIMD variant remains a probe and the production writer remains the pair-table implementation. This is not evidence that SIMD would speed up the complete formatter.

## Regular/irregular path split

Two later source shapes tested whether separating the regular and irregular `ToDecimal` paths improves code generation ([ShortRun summary](benchmark-sessions/split-paths-short.md)). Directly dispatching to separate methods reduced the regular method from **1,065 to 524 native bytes** and its stack reservation from **144 to 80 bytes**, but increased the minimal DLL by **512 B**. Two dispatch layouts measured **12.592** and **12.718 ns/value** on random inputs, above the existing compact result of **11.502 ns/value** in the paired session.

Moving common cache lookup and scaling into `ToDecimal(double)` before calling separate rounding tails kept the compact DLL at **11,776 B**. Compact decomposition stayed within earlier ranges in two sessions. The full-table profile, however, measured **8.509–8.515 ns/value** on random inputs in two sessions, compared with **7.460–7.561 ns/value** for the existing combined method. Both variants passed output agreement checks, but neither delivered a consistent speed benefit across cache profiles; the combined method was retained. Smaller native methods alone did not predict the timing.

## Shared exponent and cache placement

A narrower trial kept multiplication and rounding in their existing regular/irregular branches, but moved `ComputeDecimalExponent`, `ComputeExponentShift`, and `GetPowerOf10` before the branch. The compact conversion method fell from **1,065 to 789 native bytes**, its stack reservation from **144 to 112 bytes**, and the minimal DLL stayed **11,776 B**. Unit tests, a two-million-pattern plus exponent-boundary comparison, and the minimal-profile digest passed.

The first placement selected the decimal exponent with the `regular` flag and measured **13.902 ns/value** on random inputs, above the roughly **11.5 ns/value** of recent compact sessions. Computing the regular exponent first and overriding it for irregular values measured **11.712 ns/value** compact, but **8.684 ns/value** with the full table, above the earlier full-table **7.460–7.561 ns/value**. A branch-specific exponent calculation joined immediately before the cache lookup measured **11.969 ns/value** compact. These are single ShortRun diagnostics for each placement. None showed a consistent speed benefit across cache profiles, so the branch-local exponent, shift, and cache calculations remain in production.

## Cache access and table stride trials

The x64 `ToDecimal(ulong, int, bool)` listing has no helper call on its hot path; the compact cache is inlined. It includes bounds checks for the three table reads and reciprocal-multiply arithmetic for the stride-28 quotient. Replacing all three reads with `MemoryMarshal.GetReference` and `Unsafe.Add` removed the checks and reduced the method from **1,065 to 997 native bytes**, but one ShortRun measured **13.799 ns/value** on `Random` instead of the recent **11.4–11.8 ns/value** range. Replacing only the anchor read measured **11.619 ns/value** with a 1,041-byte method. Neither justified unchecked indexing. Moving the parity calculation later increased the method to 1,066 bytes and measured 13.500 ns/value on `Random`; shorter XOR or `andn` forms saved 5–6 native bytes but did not improve measured throughput. These expression-level trials were reverted.

A power-of-two stride was also tested with exact integer verification of all 618 reconstructed powers. Stride 16 with anchors starting at exponent -298 needs **830 raw table bytes**, versus **670** for stride 28. A shift and mask replaced the quotient arithmetic in the JIT listing, reducing the conversion method to **1,034 native bytes**, but the minimal Release DLL rose from **11,776 to 12,288 B**. The table passed cache verification, unit tests, and the two-million-pattern agreement check. ShortRun decomposition timings were:

| Cache | `LongSignificand` | `Random` | `Simple` |
|---|---:|---:|---:|
| Stride 16, run 1 | 7.773 ns | 12.510 ns | 17.882 ns |
| Stride 16, run 2 | 7.908 ns | 12.638 ns | 17.696 ns |
| Restored stride 28, subsequent run | 8.762 ns | 11.845 ns | 19.814 ns |

The stride-16 gains depend on the input set, while both random-input runs regress and the DLL is larger. The production cache remains stride 28. Native byte count and removed instructions alone did not predict throughput.
