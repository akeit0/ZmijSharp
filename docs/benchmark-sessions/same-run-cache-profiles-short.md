# Compact, full, and #131068 shortest-decomposition comparison

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. The [benchmark class](../../benchmarks/ZmijSharp.Benchmarks/CacheProfileDecompositionBenchmarks.cs) uses the [shared input generator](../../benchmarks/ZmijSharp.Benchmarks/ComponentBenchmarks.cs). The [full-cache project](../../benchmarks/ZmijSharp.Full/ZmijSharp.Full.csproj) source-links the same Żmij conversion files with `ZMIJ_FULL_TABLE` defined and has a distinct assembly name. An `extern alias` lets the benchmark call compact and full implementations in the same build. The pinned [#131068 local port](../../src/UnroundedScaling.Comparison/SOURCE.md) is the third producer.

One BenchmarkDotNet command measured all nine cases. Each case had its own benchmark process launch, three warmups, and three measured iterations. Each corpus has 10,000 deterministic finite nonzero `double` values. Setup checked that all three producers returned the same canonical significand and exponent for each value. Means are ns/value; no managed allocations were reported.

| Corpus | Żmij compact | Żmij full | #131068 local port |
|---|---:|---:|---:|
| Simple values | 18.448 | 15.525 | 15.187 |
| Varied long significands | 8.108 | 5.783 | 6.763 |
| Random raw IEEE bits | 10.175 | 6.058 | 8.880 |

The source was measured at [`d932eda`](https://github.com/akeit0/ZmijSharp/tree/d932eda). The minimal-producer DLL lengths remain 12,288 B compact and 20,480 B full; those lengths come from the [separate identical-project comparison](../size-and-assembly.md#comparable-minimal-dlls), not these benchmark assemblies. The full profile's additional 9,058 source-data bytes trade size for lower local decomposition time on these corpora. These timings exclude digit writing and formatting and are not matched CoreLib measurements.

Reproduce from the repository root with the default compact `ZmijSharp` project configuration:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "CacheProfileDecompositionBenchmarks*"
```
