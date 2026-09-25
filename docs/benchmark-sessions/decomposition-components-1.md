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
| **ZmijCanonical**      | **LongSignificand** |  **8.993 ns** | **1.0555 ns** | **0.0579 ns** |         **-** |
| UnroundedCanonical | LongSignificand |  6.764 ns | 0.1817 ns | 0.0100 ns |         - |
| UnroundedRaw       | LongSignificand |  6.225 ns | 0.1717 ns | 0.0094 ns |         - |
| **ZmijCanonical**      | **Random**          | **11.813 ns** | **3.7971 ns** | **0.2081 ns** |         **-** |
| UnroundedCanonical | Random          |  9.112 ns | 2.2923 ns | 0.1256 ns |         - |
| UnroundedRaw       | Random          |  8.574 ns | 1.4358 ns | 0.0787 ns |         - |
| **ZmijCanonical**      | **Simple**          | **19.452 ns** | **0.5872 ns** | **0.0322 ns** |         **-** |
| UnroundedCanonical | Simple          | 15.191 ns | 1.1228 ns | 0.0615 ns |         - |
| UnroundedRaw       | Simple          |  4.314 ns | 0.0727 ns | 0.0040 ns |         - |
