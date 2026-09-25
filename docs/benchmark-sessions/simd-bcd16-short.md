# Isolated 16-digit writer, two ShortRun sessions

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, BenchmarkDotNet 0.14.0. Each run used one launch, three warmups, three measured iterations, and 10,000 producer-derived 16-digit significands per invocation. Setup checked the scalar and SSE2 BCD output byte-for-byte against the pair-table writer. No managed allocations were reported. Values are means in ns/value, not complete-format timings.

| Method | Session 1 | Session 2 |
|---|---:|---:|
| Pair table | 5.749 | 5.692 |
| Scalar BCD8 | 3.719 | 3.732 |
| SSE2 BCD8 | 3.619 | 3.640 |

Reproduce with `dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*Bcd16ProbeBenchmarks*"`. The probe source is [`Bcd16ProbeBenchmarks.cs`](../../benchmarks/ZmijSharp.Benchmarks/Bcd16ProbeBenchmarks.cs).
