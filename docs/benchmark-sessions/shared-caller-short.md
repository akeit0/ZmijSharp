# Caller-side scaling setup ShortRuns

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. The baseline was `a935dd6`; the changed source was `4d5d6d2`. Both use the same power-of-two shift specialization and cache data. The changed source computes the decimal exponent, shift, and cached power in the outer `ToDecimal` entry and passes them to the private scaling and rounding method. Three measured iterations followed three warmups per benchmark. Each operation decomposes one precomputed value to a canonical significand and exponent; digit writing and presentation are excluded. Values are nanoseconds per value, with zero managed allocations reported.

| Cache / type / workload | Baseline | Caller-side setup |
|---|---:|---:|
| Compact `double` long significand | 8.932 | 8.746 |
| Compact `double` random bits | 11.345 | 10.893 |
| Compact `double` simple values | 18.955 | 18.442 |
| Compact `double` normal powers of two | 8.924 | 8.708 |
| Full `double` long significand | 6.327 | 6.011 |
| Full `double` random bits | 8.563 | 6.747 |
| Full `double` simple values | 15.659 | 15.528 |
| Full `double` normal powers of two | 6.278 | 5.948 |
| Compact `float` random bits | 9.554 | 8.035 |
| Compact `float` simple values | 11.081 | 11.395 |
| Full `float` random bits | 6.825 | 7.139 |
| Full `float` simple values | 8.494 | 8.218 |

The full-cache `double` rows are from consecutive baseline and changed-source launches. The compact `double` baseline came from the preceding session; the changed-source launch followed in the same machine session. The float benchmark source was held fixed across its baseline and changed-source launches. `Random` uses 10,000 deterministic raw-bit values with non-finite values and zero replaced; `Simple` cycles through common finite values. `LongSignificand` uses 10,000 varied binary64 values. The power-of-two corpus cycles through all normal binary64 exponents. Each row is one ShortRun per version, so smaller differences are diagnostic rather than stable speedup claims. The full-cache `double` random difference was reproduced in a second changed-source launch at 6.842 ns/value, against an earlier 8.516 ns/value baseline.

The compact and full minimal `double` DLLs remain 12,288 and 20,480 B. The listed x64 native call trees changed from 1,742 to 1,532 B compact and 1,378 to 1,355 B full; see [size and assembly](../size-and-assembly.md). The compact minimal build still matches the pinned #131068 local port's digits-and-scale digest over 249,884 inputs. Both cache profiles passed unit and oracle verification. These measurements are of local decomposition and do not imply a CoreLib performance change.

Run the current-source `double` and `float` decomposition benchmarks:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks.ZmijCanonical" "*FloatDecompositionBenchmarks*"
```

Set `$env:ZmijCache = 'Full'` before the command for the full-cache profile, then remove it afterward.
