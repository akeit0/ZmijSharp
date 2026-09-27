# PR optimization audit and double producer comparison

This branch audits [dotnet/runtime#134701 at `008a8d6`](https://github.com/dotnet/runtime/blob/008a8d6a74ca8cca6d17ae9dda898b0cf4f3e19c/src/libraries/System.Private.CoreLib/src/System/Number.Zmij.cs) against ZmijSharp and measures canonical `double` shortest-decimal decomposition only. It does not claim a text-formatting result or reproduce the PR's CoreLib build. The [runtime maintainer's comment](https://github.com/dotnet/runtime/issues/134621#issuecomment-5823161017) asks for a multi-type algorithm that also handles the precision range and says maintaining three algorithms would be unacceptable. Both xjb64 variants stay benchmark-only here.

## Applicable PR changes

| PR source choice | ZmijSharp status |
|---|---|
| Exact small-integer shortcut before decimal scaling | Imported for normal `double` values through 2^53-1; the result still goes through canonical zero normalization. |
| `BitOperations.LeadingZeroCount` | Used in the PR's **bounded-precision** path to normalize the significand. ZmijSharp does not implement that path, so there is no corresponding shortest-producer step to import. |
| Read only the high cached-power word for regular 32-bit shortest conversion | Already done by the ZmijSharp `float` core; its irregular power-of-two path needs the low word. The double regular path still needs the low word for its 128-bit fraction. |
| `Unsafe.Add` cache reads | Already used in both local compact and full cache profiles. |
| Remove trailing zeros in 8/4/2/1 chunks | An earlier local trial regressed random-input decomposition and grew the minimal DLL; retained as a research comparison rather than copied without evidence. |

The imported shortcut is compiled into the default and full-cache Żmij profiles. `ZmijSharp.PreShortcut` source-links the same core with `ZMIJ_NO_INTEGER_SHORTCUT` so the old and new producer shapes can be measured in one executable. It is a benchmark control, not a production algorithm. The standalone Release `ZmijSharp.dll` remained **22,528 bytes** before and after the shortcut; that PE length is too coarse to establish native instruction size.

## Correctness and source data

The shortcut passed `dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --double-producer 2000000`, with the existing cache identity check. The benchmark setup checks every value against the independent local unrounded-scaling canonical tuple. The benchmark-only `xjb64_v2_f64_to_dec` port is from [xjb714/xjb at `80cc895`](https://github.com/xjb714/xjb/tree/80cc89574a8f8457ffbf951afa2fd27c2459bd4a). It uses the same small-integer shortcut for a matched producer contract and normalizes trailing zeros before returning. Its 617-entry direct cache was generated from the local full table, with xjb's rounded-up low words except at exact exponents 0..55. All **1,234 generated words** matched the pinned upstream xjb table. `--producer-only --xjb-double` compared both xjb cache variants' canonical tuples for **2,035,889 finite binary64 patterns**, including exponent edges; all matched compact Żmij. This is differential testing, not a proof over all 2^64 patterns.

The xjb comparison is in dedicated `XjbSharp` projects. Both always use the direct binary32 cache. The `XJB_DOUBLE_COMPACT` compile flag selects the binary64 cache in `XjbSharp.Compact`; the default `XjbSharp` uses a direct binary64 table. The xjb64 direct cache has **617 × 16 = 9,872 raw bytes**. The local full Żmij cache has **618 × 16 = 9,888 bytes**, while the compact Żmij cache has **830 bytes**. The xjb64 compact variant reuses the same 830-byte cache and adjusts the reconstructed low word by one outside exact powers 0..55; it adds no second binary64 power table. The dedicated Release assemblies measured **16,896 bytes** (direct) and **6,656 bytes** (compact), both referencing the same production `ZmijSharp.dll`. The PR's cache is broader because its bounded-precision path shares it; these standalone sizes are not CoreLib image sizes.

## Producer-only ShortRuns

Windows 11 x64, .NET 11 RC RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun, 10,000 precomputed values per invocation, three warmups and three measured iterations. Each method returns a canonical `(significand, exponent, sign)` tuple and contributes to the same checksum; all methods allocate zero managed memory. Means are ns/value from one 24-method run.

| Corpus | Żmij compact, before shortcut | Żmij compact, after | Żmij full, after | xjb64 direct | xjb64 compact |
|---|---:|---:|---:|---:|---:|
| Integer below 2^53 | 13.04 | 2.27 | 2.26 | 2.26 | 2.44 |
| Varied long significand | 8.16 | 2.22 | 2.20 | 2.32 | 2.46 |
| Random raw bits | 10.09 | 8.55 | 8.21 | 8.01 | 11.03 |
| Simple values | 18.35 | 11.67 | 9.93 | 10.91 | 12.98 |

The direct xjb64 cache beats full Żmij by about **0.20 ns/value** on random bits in this run, is near parity on integers, and trails it on the other corpora. Compact xjb64 trails compact Żmij by **0.18–2.48 ns/value** across these corpora; the 830-byte cache therefore does not give xjb64 a speed advantage here. These are short, single-machine measurements and merit longer, multi-architecture runs before a performance claim.

The direct integer shortcut is the strong local result and uses no new cache. This comparison does not establish a compelling reason to add a second double-only implementation under the runtime maintainer's stated maintenance constraint. The PR's 32-bit types and bounded precision remain outside this standalone double producer benchmark.

Reproduce:

```powershell
python tools/generate_xjb_double_cache.py --check
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --xjb-double
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter 'ShortestDecompositionComparisonBenchmarks*'
```
