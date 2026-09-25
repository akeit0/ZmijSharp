# Stacked cache decomposition ShortRuns

Windows x64, .NET SDK `11.0.100-rc.1.26425.128`, Release `net11.0`, BenchmarkDotNet 0.14.0 ShortRun. These are separate compact and full launches on the same machine, with the pinned #131068 local canonical producer measured in each launch. Each operation converts one of 10,000 precomputed values to a canonical `(significand, exponent)` tuple; digit writing and presentation are excluded. Values are ns/operation, with three measured iterations per benchmark.

| Workload | Current Zmij compact | #131068 control in compact launch | Current Zmij full | #131068 control in full launch |
|---|---:|---:|---:|---:|
| LongSignificand | 8.933 | 6.813 | 6.417 | 6.608 |
| Random | 11.320 | 9.104 | 8.541 | 9.098 |
| Simple | 18.966 | 15.152 | 15.739 | 15.156 |

The compact build uses 39 stride-16 anchor pairs, 16 minor words, and 78 correction bytes. Both builds use unchecked table reads backed by the internal exponent-range invariant. The full build reads adjacent words from the 618-pair table. These timings do not estimate a CoreLib image delta or a matched runtime build.

Reproduce compact:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks*"
```

Reproduce full:

```powershell
$env:ZmijCache = 'Full'
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks*"
Remove-Item Env:ZmijCache
```
