```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method       | Workload        | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------- |---------------- |----------:|----------:|----------:|------:|----------:|------------:|
| **Baseline**     | **LongSignificand** |  **8.363 ns** | **1.0656 ns** | **0.0584 ns** |  **1.00** |         **-** |          **NA** |
| Consolidated | LongSignificand |  8.269 ns | 0.2549 ns | 0.0140 ns |  0.99 |         - |          NA |
|              |                 |           |           |           |       |           |             |
| **Baseline**     | **Random**          | **11.596 ns** | **0.4089 ns** | **0.0224 ns** |  **1.00** |         **-** |          **NA** |
| Consolidated | Random          | 11.390 ns | 1.3961 ns | 0.0765 ns |  0.98 |         - |          NA |
|              |                 |           |           |           |       |           |             |
| **Baseline**     | **Simple**          | **15.227 ns** | **0.9000 ns** | **0.0493 ns** |  **1.00** |         **-** |          **NA** |
| Consolidated | Simple          | 15.628 ns | 1.3622 ns | 0.0747 ns |  1.03 |         - |          NA |
