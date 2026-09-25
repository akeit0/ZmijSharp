```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                             | Workload        | Mean     | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|----------------------------------- |---------------- |---------:|---------:|---------:|------:|----------:|------------:|
| **Zmij_SignificantDigits**             | **Extreme**         | **18.54 ns** | **0.860 ns** | **0.047 ns** |  **0.38** |         **-** |          **NA** |
| Runtime_TryFormat                  | Extreme         | 49.22 ns | 1.841 ns | 0.101 ns |  1.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Extreme         | 15.89 ns | 0.817 ns | 0.045 ns |  0.32 |         - |          NA |
| UnroundedScaling_TryFormat         | Extreme         | 29.95 ns | 0.939 ns | 0.051 ns |  0.61 |         - |          NA |
| Zmij_TryFormat                     | Extreme         | 29.68 ns | 3.033 ns | 0.166 ns |  0.60 |         - |          NA |
|                                    |                 |          |          |          |       |           |             |
| **Zmij_SignificantDigits**             | **JsonLike**        | **21.82 ns** | **0.797 ns** | **0.044 ns** |  **0.71** |         **-** |          **NA** |
| Runtime_TryFormat                  | JsonLike        | 30.71 ns | 1.898 ns | 0.104 ns |  1.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | JsonLike        | 16.70 ns | 3.189 ns | 0.175 ns |  0.54 |         - |          NA |
| UnroundedScaling_TryFormat         | JsonLike        | 28.01 ns | 1.368 ns | 0.075 ns |  0.91 |         - |          NA |
| Zmij_TryFormat                     | JsonLike        | 30.23 ns | 2.365 ns | 0.130 ns |  0.98 |         - |          NA |
|                                    |                 |          |          |          |       |           |             |
| **Zmij_SignificantDigits**             | **LongSignificand** | **13.64 ns** | **1.423 ns** | **0.078 ns** |  **0.21** |         **-** |          **NA** |
| Runtime_TryFormat                  | LongSignificand | 64.54 ns | 7.482 ns | 0.410 ns |  1.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | LongSignificand | 18.63 ns | 1.900 ns | 0.104 ns |  0.29 |         - |          NA |
| UnroundedScaling_TryFormat         | LongSignificand | 33.67 ns | 0.888 ns | 0.049 ns |  0.52 |         - |          NA |
| Zmij_TryFormat                     | LongSignificand | 24.37 ns | 1.317 ns | 0.072 ns |  0.38 |         - |          NA |
|                                    |                 |          |          |          |       |           |             |
| **Zmij_SignificantDigits**             | **Random**          | **22.51 ns** | **5.178 ns** | **0.284 ns** |  **0.25** |         **-** |          **NA** |
| Runtime_TryFormat                  | Random          | 91.12 ns | 4.600 ns | 0.252 ns |  1.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Random          | 21.72 ns | 1.199 ns | 0.066 ns |  0.24 |         - |          NA |
| UnroundedScaling_TryFormat         | Random          | 50.14 ns | 0.905 ns | 0.050 ns |  0.55 |         - |          NA |
| Zmij_TryFormat                     | Random          | 44.87 ns | 1.371 ns | 0.075 ns |  0.49 |         - |          NA |
|                                    |                 |          |          |          |       |           |             |
| **Zmij_SignificantDigits**             | **Simple**          | **19.76 ns** | **2.257 ns** | **0.124 ns** |  **0.58** |         **-** |          **NA** |
| Runtime_TryFormat                  | Simple          | 33.83 ns | 4.518 ns | 0.248 ns |  1.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Simple          | 15.93 ns | 0.692 ns | 0.038 ns |  0.47 |         - |          NA |
| UnroundedScaling_TryFormat         | Simple          | 27.60 ns | 2.462 ns | 0.135 ns |  0.82 |         - |          NA |
| Zmij_TryFormat                     | Simple          | 27.84 ns | 2.527 ns | 0.139 ns |  0.82 |         - |          NA |
