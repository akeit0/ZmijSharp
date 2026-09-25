# Compact versus full Zmij power cache

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC runtime, BenchmarkDotNet 0.14.0 ShortRun. The sequence was full cache, compact cache, full cache. Each session used one launch, three warmups, three measured iterations, and 10,000 finite nonzero `double` values per invocation. The benchmark checks every Zmij canonical tuple against the pinned #131068 local port before timing. Values are means in ns/value; no managed allocations were reported.

| Workload | Full session 1 | Compact session | Full session 2 | Local #131068 canonical control, three sessions |
|---|---:|---:|---:|---:|
| LongSignificand | 6.118 | 8.924 | 6.148 | 6.597, 6.556, 6.746 |
| Random | 7.460 | 11.502 | 7.561 | 8.951, 8.985, 9.005 |
| Simple | 15.942 | 19.310 | 15.637 | 15.266, 15.266, 15.090 |

The full table removes compact-cache reconstruction from `ToDecimal`; the complete decomposition still includes binary decoding, scaling, rounding, and decimal trailing-zero normalization. These are separate process runs, so their difference is a cache-profile effect in this harness rather than a per-stage stopwatch measurement or a CoreLib result.

Reproduce either profile from the repository root:

```powershell
$env:ZmijCache = 'Full'
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter '*DecompositionBenchmarks*'
Remove-Item Env:ZmijCache
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter '*DecompositionBenchmarks*'
```
