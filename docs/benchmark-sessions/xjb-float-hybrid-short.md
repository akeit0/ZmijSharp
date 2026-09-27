# Binary32 xjb and hybrid cache prototype

Historical measurement: the xjb32 compact-cache variant used in this session was removed from the active comparison project. `benchmarks/XjbSharp` now always uses the direct binary32 cache; its separate compact profile changes only the binary64 cache. The numbers below describe the earlier source shape.

Branch `codex/xjb-float-cache-comparison` compares four canonical `float` shortest-decimal producers. The xjb port is adapted from [xjb714/xjb at `80cc895`](https://github.com/xjb714/xjb/tree/80cc89574a8f8457ffbf951afa2fd27c2459bd4a), specifically `xjb_comp_f32_to_dec` and the direct binary32 cache in `bench/xjb/float_to_decimal/xjb32_i.cpp`. Both xjb variants normalize trailing decimal zeros before returning a `ZmijDecimal`, matching the Zmij contract. They are compiled only into the benchmark and verification programs. The [xjb paper](https://doi.org/10.3390/computers15050280) reports native conversion results; the measurements below are this repository's .NET port and workload.

The hybrid profile makes `float` read a direct, exact 128-bit cache for exponents -32 through 44. `double` continues using the existing compact cache. Build the library with `-p:ZmijCache=FloatFull`; the default remains compact for both types. The additional cache consists of 77 entries copied from `Pow10Tables.cs`. Its arithmetic and writer are unchanged. This is relevant to [dotnet/runtime#134701](https://github.com/dotnet/runtime/pull/134701), though these are standalone ZmijSharp results, not a CoreLib build.

## Correctness

`dotnet run -c Release --project tests/ZmijSharp.Verify -- --xjb-float` compared both xjb variants and the hybrid against compact Zmij on **1,996,743 finite binary32 bit patterns**: 2 million deterministic raw patterns plus positive and negative exponent-edge cases. Each comparison used canonical significand, decimal exponent, and sign. It also compared hybrid and compact on 100,000 raw binary64 patterns. The first 10,000 binary32 patterns compared hybrid UTF-8 output and short-buffer UTF-16 behavior. The normal verification suite passed, including random output checks and exact-integer oracle checks. `dotnet test -c Release --no-restore ZmijSharp.slnx` passed 11 unit tests. This is broad differential testing, not an exhaustive binary32 proof.

## Cache and assembly size

| Profile | Counted cached-power constants | Release `ZmijSharp.dll` |
|---|---:|---:|
| Zmij compact for both types | 830 B | 22,528 B |
| Zmij full `float`, compact `double` | 830 + 1,232 = 2,062 B | 24,064 B |
| Zmij full shared cache | 9,888 B | 30,720 B |
| xjb32 compact producer | 64 B binary32 cache | Not isolated |
| xjb32 direct producer | 616 B binary32 cache | Not isolated |

The three DLLs were built from `src/ZmijSharp/ZmijSharp.csproj` with `dotnet build -c Release -o <separate directory>`, using default, `-p:ZmijCache=FloatFull`, and `-p:ZmijCache=Full` respectively. Their file lengths include metadata, IL, and PE alignment; the constant counts are raw source data. The xjb comparison code is in the benchmark assembly, so its total image-size cost is not established by the 64/616-byte cache figures. None of these values measures incremental CoreLib ReadyToRun or NativeAOT size.

## Performance

Windows 11 x64, .NET 11 RC RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun, three warmups and three measured iterations. Each call consumes one of 10,000 precomputed values, computes a canonical tuple, and contributes to a checksum. `Random` is deterministic raw binary32 data with nonfinite/zero replacements; `Simple` cycles through common values. All producers ran in the same benchmark executable; each method got its own process. Means are ns/value, zero managed allocation:

| Producer | Random | Simple |
|---|---:|---:|
| Zmij compact | 7.685 | 10.985 |
| Zmij hybrid | 7.169 | 8.362 |
| xjb32 compact | 7.645 | 10.095 |
| xjb32 direct | 3.934 | 7.756 |

The earlier eight-method run measured compact Zmij **7.950 / 10.999**, hybrid **7.265 / 8.548**, xjb compact **5.997 / 10.246**, and xjb direct **3.921 / 7.804** ns/value for Random / Simple. A two-method rerun measured compact Zmij **7.833 / 11.001** and xjb compact **5.756 / 10.142**. An earlier compact-only xjb source shape measured **10.160 / 11.712**. The xjb compact Random result is sensitive to source shape and benchmark composition, so its ranking is unresolved. The direct xjb and hybrid results were more consistent across the two eight-method runs. These short runs identify promising experiments, not a portable speedup claim.

The same hybrid profile was measured through the complete ZmijSharp writer. A 10,000-value finite binary32 corpus produced byte-identical UTF-8 strings during setup. The benchmark includes conversion and presentation with invariant-style default formatting:

| Format path | Compact | Hybrid |
|---|---:|---:|
| UTF-8 `TryFormat` | 32.69 ns | 29.90 ns |
| UTF-16 `TryFormat` | 37.02 ns | 34.82 ns |

The hybrid adds **1,536 B to this standalone DLL** over compact while keeping `double` compact, and improved local complete-formatting by about 2–3 ns/value. The xjb producer has not yet been integrated with the common writer, so its producer times are not full-formatting times. Before selecting an implementation for CoreLib, measure these profiles in the PR with its actual shared table, direct invariant writer, bounded-precision path, and target architectures.

Reproduce with:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter 'FloatDecompositionBenchmarks*'
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter 'FloatHybridFormatBenchmarks*'
```
