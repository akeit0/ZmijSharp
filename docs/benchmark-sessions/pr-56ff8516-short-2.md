```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                             | Workload        | Mean     | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------------- |---------------- |---------:|----------:|---------:|------:|--------:|----------:|------------:|
| **Zmij_SignificantDigits**             | **Extreme**         | **19.17 ns** |  **1.927 ns** | **0.106 ns** |  **0.39** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | Extreme         | 49.51 ns |  8.655 ns | 0.474 ns |  1.00 |    0.01 |         - |          NA |
| UnroundedScaling_SignificantDigits | Extreme         | 16.48 ns |  7.106 ns | 0.389 ns |  0.33 |    0.01 |         - |          NA |
| UnroundedScaling_TryFormat         | Extreme         | 30.89 ns |  6.691 ns | 0.367 ns |  0.62 |    0.01 |         - |          NA |
| Zmij_TryFormat                     | Extreme         | 29.18 ns |  0.694 ns | 0.038 ns |  0.59 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **JsonLike**        | **21.86 ns** |  **2.579 ns** | **0.141 ns** |  **0.72** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | JsonLike        | 30.36 ns |  0.441 ns | 0.024 ns |  1.00 |    0.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | JsonLike        | 16.73 ns |  0.806 ns | 0.044 ns |  0.55 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | JsonLike        | 28.68 ns |  2.505 ns | 0.137 ns |  0.94 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | JsonLike        | 30.10 ns |  0.361 ns | 0.020 ns |  0.99 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **LongSignificand** | **17.57 ns** |  **0.772 ns** | **0.042 ns** |  **0.27** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | LongSignificand | 64.91 ns |  7.408 ns | 0.406 ns |  1.00 |    0.01 |         - |          NA |
| UnroundedScaling_SignificantDigits | LongSignificand | 18.96 ns |  5.730 ns | 0.314 ns |  0.29 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | LongSignificand | 39.79 ns | 13.000 ns | 0.713 ns |  0.61 |    0.01 |         - |          NA |
| Zmij_TryFormat                     | LongSignificand | 24.68 ns |  5.088 ns | 0.279 ns |  0.38 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **Random**          | **24.36 ns** |  **3.439 ns** | **0.189 ns** |  **0.27** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | Random          | 89.46 ns |  8.057 ns | 0.442 ns |  1.00 |    0.01 |         - |          NA |
| UnroundedScaling_SignificantDigits | Random          | 23.05 ns |  7.022 ns | 0.385 ns |  0.26 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | Random          | 51.38 ns |  1.336 ns | 0.073 ns |  0.57 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | Random          | 45.48 ns |  0.779 ns | 0.043 ns |  0.51 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **Simple**          | **20.17 ns** |  **2.530 ns** | **0.139 ns** |  **0.58** |    **0.01** |         **-** |          **NA** |
| Runtime_TryFormat                  | Simple          | 34.77 ns | 11.526 ns | 0.632 ns |  1.00 |    0.02 |         - |          NA |
| UnroundedScaling_SignificantDigits | Simple          | 15.47 ns |  0.334 ns | 0.018 ns |  0.45 |    0.01 |         - |          NA |
| UnroundedScaling_TryFormat         | Simple          | 30.34 ns |  0.801 ns | 0.044 ns |  0.87 |    0.01 |         - |          NA |
| Zmij_TryFormat                     | Simple          | 27.95 ns |  1.177 ns | 0.064 ns |  0.80 |    0.01 |         - |          NA |
