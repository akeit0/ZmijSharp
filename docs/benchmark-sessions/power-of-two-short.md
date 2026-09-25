# Normal-power-of-two decomposition ShortRuns

Windows x64, .NET SDK `11.0.100-rc.1.26425.128`, Release `net11.0`, BenchmarkDotNet 0.14.0 ShortRun. Each operation converts one of 10,000 precomputed normal powers of two to a canonical `(significand, exponent)` result. The double corpus cycles through all 2,046 normal binary64 exponents; the float corpus cycles through all 254 normal binary32 exponents. Each A/B pair used the same benchmark source and switched only the irregular-scaling implementation. Means are nanoseconds per value from three measured iterations per benchmark in separate launches.

| Profile and operation | Shared multiplication | Power-of-two shift |
|---|---:|---:|
| Compact `double` | 9.508 | 9.038 |
| Compact `float` | 9.240 | 8.681 |
| Full `double` | 6.649 | 6.182 |

The compact `double` and `float` rows were measured together in each launch. The full `double` pair came from separate two-method launches. The local #131068 `double` control was 5.689 and 5.568 ns/value in the compact baseline and modified launches, and 5.596 and 5.562 in the full pair. There is no local #131068 `float` benchmark. The setup checks that Zmij and the local #131068 port produce identical canonical `double` results for the corpus.

These results measure the changed branch directly. They do not measure overall formatting, the prevalence of powers of two in applications, or a CoreLib integration. Independent correctness checks cover random inputs and all binary-exponent boundaries.

Reproduce the modified compact source:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*PowerOfTwoDecompositionBenchmarks*"
```

Set `$env:ZmijCache = 'Full'` for the full-cache profile, then remove the variable after the run.
