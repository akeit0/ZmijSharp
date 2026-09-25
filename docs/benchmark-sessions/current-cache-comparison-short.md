# Earlier compact/full shortest-decomposition comparison

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. This measures the caller-side scaling source at `1e600aa`, before the [nonzero-last-digit change](nonzero-last-digit-short.md), in compact and full-cache builds against the pinned local `double` specialization of [dotnet/runtime#131068 at `56ff8516`](../../src/UnroundedScaling.Comparison/SOURCE.md). Each benchmark converts one of 10,000 precomputed finite nonzero `double` values to a canonical significand and exponent. Setup checks that both producers return the same tuple for every value. Three measured iterations follow three warmups. Values are ns/value; no managed allocations were reported. Each Zmij profile and its #131068 control ran in one launch; the compact and full launches were separate.

| Input corpus | Zmij compact | UnroundedScaling, compact launch | Zmij full | UnroundedScaling, full launch |
|---|---:|---:|---:|---:|
| Simple values | 19.033 | 15.524 | 15.493 | 15.161 |
| Varied long significands | 8.510 | 6.585 | 6.177 | 6.632 |
| Random raw IEEE bits | 10.915 | 8.934 | 6.760 | 8.764 |

The compact Zmij profile was slower than the local #131068 producer on each corpus. In the full-cache launch, Zmij was faster on varied long significands and random bits and close on simple values. The full cache uses 9,888 source-data bytes versus 830 compact and adds 8,192 B to the minimal Zmij DLL. These methods stop at canonical decomposition: they do not write digits or format text, and the port is a `double` specialization rather than a matched CoreLib build. The [caller-placement A/B table](shared-caller-short.md) answers the separate question of how much the source movement changed Zmij itself.

Reproduce the current compact session:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks.ZmijCanonical" "*DecompositionBenchmarks.UnroundedCanonical"
```

For the full session, set `$env:ZmijCache = 'Full'`, run the same command, and remove the variable afterward.
