```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method      | Workload        | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------ |---------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Zmij**        | **LongSignificand** |  **6.033 ns** | **0.0597 ns** | **0.0033 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CoreLibPort | LongSignificand |  7.017 ns | 0.6272 ns | 0.0344 ns |  1.16 |    0.00 |         - |          NA |
|             |                 |           |           |           |       |         |           |             |
| **Zmij**        | **Random**          | **10.652 ns** | **1.0731 ns** | **0.0588 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| CoreLibPort | Random          | 11.780 ns | 0.5922 ns | 0.0325 ns |  1.11 |    0.01 |         - |          NA |
|             |                 |           |           |           |       |         |           |             |
| **Zmij**        | **Simple**          |  **2.360 ns** | **0.7743 ns** | **0.0424 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| CoreLibPort | Simple          |  2.409 ns | 0.5062 ns | 0.0277 ns |  1.02 |    0.02 |         - |          NA |
