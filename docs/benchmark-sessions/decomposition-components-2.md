```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Workload        | Mean      | Error     | StdDev    | Allocated |
|------------------- |---------------- |----------:|----------:|----------:|----------:|
| **ZmijCanonical**      | **LongSignificand** |  **9.054 ns** | **1.1742 ns** | **0.0644 ns** |         **-** |
| UnroundedCanonical | LongSignificand |  6.812 ns | 1.5138 ns | 0.0830 ns |         - |
| UnroundedRaw       | LongSignificand |  6.352 ns | 0.8323 ns | 0.0456 ns |         - |
| **ZmijCanonical**      | **Random**          | **11.588 ns** | **0.5329 ns** | **0.0292 ns** |         **-** |
| UnroundedCanonical | Random          |  8.510 ns | 2.1070 ns | 0.1155 ns |         - |
| UnroundedRaw       | Random          |  8.395 ns | 1.2572 ns | 0.0689 ns |         - |
| **ZmijCanonical**      | **Simple**          | **19.450 ns** | **1.0901 ns** | **0.0598 ns** |         **-** |
| UnroundedCanonical | Simple          | 14.990 ns | 0.6380 ns | 0.0350 ns |         - |
| UnroundedRaw       | Simple          |  4.303 ns | 0.3656 ns | 0.0200 ns |         - |
