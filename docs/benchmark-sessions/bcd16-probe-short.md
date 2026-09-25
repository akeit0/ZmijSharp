```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.9457)
Unknown processor
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2
  ShortRun : .NET 11.0.0 (11.0.26.42628), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method      | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------ |---------:|----------:|----------:|------:|----------:|------------:|
| PairTable   | 5.698 ns | 0.3921 ns | 0.0215 ns |  1.00 |         - |          NA |
| OriginalBcd | 3.689 ns | 0.3203 ns | 0.0176 ns |  0.65 |         - |          NA |
