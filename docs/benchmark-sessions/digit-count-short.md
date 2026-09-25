```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method      | Workload        | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------ |---------------- |----------:|----------:|----------:|------:|----------:|------------:|
| **Zmij**        | **LongSignificand** | **0.5066 ns** | **0.0367 ns** | **0.0020 ns** |  **1.00** |         **-** |          **NA** |
| CoreLibPort | LongSignificand | 0.6005 ns | 0.0966 ns | 0.0053 ns |  1.19 |         - |          NA |
|             |                 |           |           |           |       |           |             |
| **Zmij**        | **Random**          | **0.5146 ns** | **0.1017 ns** | **0.0056 ns** |  **1.00** |         **-** |          **NA** |
| CoreLibPort | Random          | 0.5993 ns | 0.0628 ns | 0.0034 ns |  1.16 |         - |          NA |
|             |                 |           |           |           |       |           |             |
| **Zmij**        | **Simple**          | **0.5170 ns** | **0.1011 ns** | **0.0055 ns** |  **1.00** |         **-** |          **NA** |
| CoreLibPort | Simple          | 0.6034 ns | 0.1072 ns | 0.0059 ns |  1.17 |         - |          NA |
