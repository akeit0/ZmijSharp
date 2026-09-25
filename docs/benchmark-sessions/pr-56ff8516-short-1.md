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
| **Zmij_SignificantDigits**             | **Extreme**         | **19.16 ns** |  **1.428 ns** | **0.078 ns** |  **0.39** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | Extreme         | 49.03 ns |  1.948 ns | 0.107 ns |  1.00 |    0.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Extreme         | 15.95 ns |  0.689 ns | 0.038 ns |  0.33 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | Extreme         | 29.83 ns |  1.376 ns | 0.075 ns |  0.61 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | Extreme         | 29.08 ns |  1.920 ns | 0.105 ns |  0.59 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **JsonLike**        | **21.82 ns** |  **0.531 ns** | **0.029 ns** |  **0.71** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | JsonLike        | 30.61 ns |  0.616 ns | 0.034 ns |  1.00 |    0.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | JsonLike        | 16.42 ns |  0.572 ns | 0.031 ns |  0.54 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | JsonLike        | 28.08 ns |  1.804 ns | 0.099 ns |  0.92 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | JsonLike        | 29.93 ns |  0.599 ns | 0.033 ns |  0.98 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **LongSignificand** | **17.41 ns** |  **0.200 ns** | **0.011 ns** |  **0.26** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | LongSignificand | 66.31 ns |  7.233 ns | 0.396 ns |  1.00 |    0.01 |         - |          NA |
| UnroundedScaling_SignificantDigits | LongSignificand | 18.46 ns |  1.060 ns | 0.058 ns |  0.28 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | LongSignificand | 33.57 ns |  1.876 ns | 0.103 ns |  0.51 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | LongSignificand | 24.40 ns |  0.568 ns | 0.031 ns |  0.37 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **Random**          | **24.44 ns** |  **1.574 ns** | **0.086 ns** |  **0.28** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | Random          | 88.58 ns |  5.358 ns | 0.294 ns |  1.00 |    0.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Random          | 21.45 ns |  1.574 ns | 0.086 ns |  0.24 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | Random          | 49.82 ns |  6.374 ns | 0.349 ns |  0.56 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | Random          | 44.72 ns |  2.349 ns | 0.129 ns |  0.50 |    0.00 |         - |          NA |
|                                    |                 |          |           |          |       |         |           |             |
| **Zmij_SignificantDigits**             | **Simple**          | **20.11 ns** |  **0.610 ns** | **0.033 ns** |  **0.60** |    **0.00** |         **-** |          **NA** |
| Runtime_TryFormat                  | Simple          | 33.25 ns |  1.065 ns | 0.058 ns |  1.00 |    0.00 |         - |          NA |
| UnroundedScaling_SignificantDigits | Simple          | 15.92 ns |  3.191 ns | 0.175 ns |  0.48 |    0.00 |         - |          NA |
| UnroundedScaling_TryFormat         | Simple          | 27.41 ns |  3.068 ns | 0.168 ns |  0.82 |    0.00 |         - |          NA |
| Zmij_TryFormat                     | Simple          | 30.17 ns | 11.973 ns | 0.656 ns |  0.91 |    0.02 |         - |          NA |
