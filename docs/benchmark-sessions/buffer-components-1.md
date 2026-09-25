```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                 | Workload        | Mean     | Error    | StdDev   | Allocated |
|----------------------- |---------------- |---------:|---------:|---------:|----------:|
| **PointerBuffer**          | **LongSignificand** | **12.15 ns** | **1.439 ns** | **0.079 ns** |         **-** |
| SpanBuffer             | LongSignificand | 12.15 ns | 0.705 ns | 0.039 ns |         - |
| PointerBufferThenWiden | LongSignificand | 20.53 ns | 0.373 ns | 0.020 ns |         - |
| **PointerBuffer**          | **Random**          | **15.09 ns** | **2.185 ns** | **0.120 ns** |         **-** |
| SpanBuffer             | Random          | 15.07 ns | 2.028 ns | 0.111 ns |         - |
| PointerBufferThenWiden | Random          | 26.96 ns | 1.251 ns | 0.069 ns |         - |
| **PointerBuffer**          | **Simple**          | **12.01 ns** | **2.416 ns** | **0.132 ns** |         **-** |
| SpanBuffer             | Simple          | 11.68 ns | 2.672 ns | 0.146 ns |         - |
| PointerBufferThenWiden | Simple          | 16.41 ns | 4.288 ns | 0.235 ns |         - |
