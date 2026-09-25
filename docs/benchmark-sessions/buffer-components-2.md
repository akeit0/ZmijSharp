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
| **PointerBuffer**          | **LongSignificand** | **12.05 ns** | **0.274 ns** | **0.015 ns** |         **-** |
| SpanBuffer             | LongSignificand | 11.99 ns | 1.264 ns | 0.069 ns |         - |
| PointerBufferThenWiden | LongSignificand | 20.63 ns | 1.959 ns | 0.107 ns |         - |
| **PointerBuffer**          | **Random**          | **15.33 ns** | **1.845 ns** | **0.101 ns** |         **-** |
| SpanBuffer             | Random          | 15.28 ns | 3.090 ns | 0.169 ns |         - |
| PointerBufferThenWiden | Random          | 26.80 ns | 1.808 ns | 0.099 ns |         - |
| **PointerBuffer**          | **Simple**          | **11.69 ns** | **0.124 ns** | **0.007 ns** |         **-** |
| SpanBuffer             | Simple          | 11.34 ns | 0.385 ns | 0.021 ns |         - |
| PointerBufferThenWiden | Simple          | 16.27 ns | 1.444 ns | 0.079 ns |         - |
