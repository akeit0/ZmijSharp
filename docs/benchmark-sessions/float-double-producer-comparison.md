# Float and double producer comparison

This branch audits [dotnet/runtime#134701 at `008a8d6`](https://github.com/dotnet/runtime/blob/008a8d6a74ca8cca6d17ae9dda898b0cf4f3e19c/src/libraries/System.Private.CoreLib/src/System/Number.Zmij.cs) against ZmijSharp and measures canonical `float` and `double` shortest-decimal decomposition. It does not claim a text-formatting result or reproduce the PR's CoreLib build. The [runtime maintainer's comment](https://github.com/dotnet/runtime/issues/134621#issuecomment-5823161017) asks for a multi-type algorithm that also handles the precision range and says maintaining three algorithms would be unacceptable. xjb stays in dedicated comparison projects.

## Applicable PR changes

| PR source choice | ZmijSharp status |
|---|---|
| Exact small-integer shortcut before decimal scaling | Imported for normal `float` values through 2^24-1 and normal `double` values through 2^53-1; results go through canonical zero normalization. The benchmark-only xjb ports use the same shortcut. |
| `BitOperations.LeadingZeroCount` | Used in the PR's **bounded-precision** path to normalize the significand. ZmijSharp does not implement that path, so there is no corresponding shortest-producer step to import. |
| Read only the high cached-power word for regular 32-bit shortest conversion | Already done by the ZmijSharp `float` core; its irregular power-of-two path needs the low word. The double regular path still needs the low word for its 128-bit fraction. |
| `Unsafe.Add` cache reads | Already used in both local compact and full cache profiles. |
| Remove trailing zeros in 8/4/2/1 chunks | An earlier local trial regressed random-input decomposition and grew the minimal DLL; retained as a research comparison rather than copied without evidence. |

The integer shortcut is unconditional in the Żmij cores. The pre-shortcut build profile and benchmark row have been removed. The standalone Release `ZmijSharp.dll` is **22,528 bytes**; PE length is too coarse to establish native instruction size.

## Correctness and source data

The shortcut passed the producer sweep and existing cache identity check. Each benchmark setup checks the four producers' canonical tuples for its 10,000 values; the double setup also uses the independent local unrounded-scaling oracle. The benchmark-only xjb ports come from [xjb714/xjb at `80cc895`](https://github.com/xjb714/xjb/tree/80cc89574a8f8457ffbf951afa2fd27c2459bd4a) and normalize trailing zeros before returning. The binary32 path now adapts upstream `xjb_v2_f32_to_dec`; its 256-byte exponent-indexed shift table matched upstream for all 255 finite exponents. The 617-entry xjb64 direct cache was generated from the local full table, with xjb's rounded-up low words except at exact exponents 0..55. All **1,234 generated words** matched the pinned upstream xjb table. Verification compared the xjb variants with compact Żmij on **1,996,743 finite binary32** and **2,035,889 finite binary64** patterns, including exponent edges. This is differential testing, not an exhaustive proof.

The xjb comparison is in dedicated `XjbSharp` projects. Both always use the direct binary32 cache. The `XJB_DOUBLE_COMPACT` compile flag selects the binary64 cache in `XjbSharp.Compact`; default `XjbSharp` uses a direct binary64 table. Compact xjb reconstructs the Żmij binary64 power and adjusts the low word by one outside exact powers 0..55.

## Lookup data size

| Measure | Żmij compact | Żmij full | xjb direct | xjb compact |
|---|---:|---:|---:|---:|
| Binary32 power data | Shared table | Shared table | 616 B | 616 B |
| Binary32 shift table | None | None | 256 B | 256 B |
| Binary64 power data | Shared table | Shared table | 9,872 B | Reuses Żmij compact |
| Distinct lookup data used | 830 B | 9,888 B | 10,744 B | 1,702 B shared across DLLs |

Żmij's power table serves both widths. xjb's two widths use separate power tables; compact xjb's 1,702 B is 616 B of float powers and 256 B of shifts in its own DLL, plus the existing 830 B table in `ZmijSharp.dll`. These are raw constants used by each profile, without PE alignment, metadata, IL, or native code. Whole-assembly file lengths were removed from this table because the Żmij DLLs include formatting code while the xjb DLLs depend on `ZmijSharp.dll`. A four-profile build with the same producer-only boundary is needed for a fair code or DLL size comparison. The PR's shared table also serves bounded precision; these counts are not a CoreLib image size.

## Producer-only ShortRun

Windows 11 x64, .NET 11 RC RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun, 10,000 precomputed values per invocation, three warmups and three measured iterations. Each method returns a canonical `(significand, exponent, sign)` tuple and contributes to the same checksum; all methods allocate zero managed memory. Means are ns/value from one 24-method run. The `float` and `double` corpora are type-specific; Random and Simple describe their construction, not identical numeric values.

| Type and corpus | Żmij compact | Żmij full | xjb direct | xjb compact |
|---|---:|---:|---:|---:|
| `float` Random | 9.62 | 5.66 | 5.71 | 5.71 |
| `float` Simple | 7.50 | 5.72 | 5.49 | 5.50 |
| `double` Integer below 2^53 | 2.20 | 2.20 | 2.36 | 2.46 |
| `double` LongSignificand | 2.32 | 2.27 | 2.26 | 2.40 |
| `double` Random | 8.96 | 8.34 | 8.22 | 11.38 |
| `double` Simple | 11.81 | 10.03 | 10.58 | 12.99 |

The xjb direct and xjb compact projects compile the **same v2 direct-cache `float` source**; their float times are nearly identical. Against the previous xjb32 port's 6.98 / 6.48 ns Random / Simple in a separate ShortRun, v2's 5.71 / 5.49 suggests a useful improvement. In the matched run above, v2 and full-cache Żmij differ by only **0.05 ns/value** on Random; v2 leads by **0.23 ns/value** on Simple. On random `double`, full-cache Żmij and direct xjb differ by **0.13 ns/value**, while compact xjb trails compact Żmij by **2.42 ns/value**. These are short, single-machine measurements and merit longer, multi-architecture runs before a performance claim.

The accepted integer shortcut uses no new cache. This comparison does not establish a compelling reason to add a second algorithm under the runtime maintainer's stated maintenance constraint. Half, BFloat16, bounded precision, and the complete CoreLib formatting path remain outside this producer benchmark.

Reproduce:

```powershell
python tools/generate_xjb_double_cache.py --check
python tools/generate_xjb_float_h37.py --check
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --xjb-float --xjb-double --double-producer 2000000
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter '*FloatDecompositionBenchmarks*' '*ShortestDecompositionComparisonBenchmarks*'
```
