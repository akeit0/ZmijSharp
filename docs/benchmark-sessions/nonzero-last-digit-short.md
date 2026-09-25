# Nonzero last digit in `ToDecimal(double)`

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, .NET 11 RC X64 RyuJIT AVX2, BenchmarkDotNet 0.14.0 ShortRun. The change at [`d0ee1bc`](https://github.com/akeit0/ZmijSharp/tree/d0ee1bc) skips trailing-zero division when the extra decimal digit is nonzero. That digit proves the result is already canonical. The alternate trial that folded a zero extra digit into the exponent measured 8.434 / 10.456 / 18.599 ns/value in compact and 5.548 / 7.007 / 15.621 in full for long / random / simple corpora; the retained version was better overall.

Each corpus contains 10,000 deterministic finite nonzero `double` values. Results are ns/value, with three warmups and three measured iterations. Baseline and changed-source runs were separate launches, so small differences may be run noise. Setup checks canonical tuple agreement against the pinned local #131068 port. No managed allocations were reported.

| Corpus | Compact before | Compact after | Full before | Full after |
|---|---:|---:|---:|---:|
| Varied long significands | 8.591 | 8.229 | 6.060 | 5.605 |
| Random raw IEEE bits | 11.046 | 10.450 | 6.764 | 6.581 |
| Simple values | 18.792 | 18.551 | 15.514 | 15.466 |

The separate paired comparison runs at the retained revision measured both producers in each cache launch:

| Corpus | Zmij compact | #131068 port, compact launch | Zmij full | #131068 port, full launch |
|---|---:|---:|---:|---:|
| Varied long significands | 8.105 | 6.541 | 5.752 | 6.647 |
| Random raw IEEE bits | 10.124 | 9.043 | 6.232 | 8.787 |
| Simple values | 18.889 | 15.401 | 15.539 | 15.171 |

The compact minimal DLL remains 12,288 B and the full one 20,480 B. With tiering disabled, x64 RyuJIT emits 606 B for `ToDecimal(double)` in compact and 427 B in full, up from 576 B and 399 B. The private scale-and-round method remains 486 B. The entry's nonzero-digit branch returns before the reciprocal-multiply `% 10` loop; the loop remains for zero or absent extra digits. The extra native bytes do not change the PE lengths.

Both profiles passed 11 unit tests and produced the same digits-and-scale SHA-256 digest over 249,884 finite nonzero inputs as the pinned port: `14EDF7E7D588C49697EB4AF85A8F803C564A008A73A25029FC5941A198D1BCCF`. The compact profile also passed a two-million-pattern plus exponent-boundary comparison.

Reproduce the paired compact run:

```powershell
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "DecompositionBenchmarks.ZmijCanonical*" "DecompositionBenchmarks.UnroundedCanonical*"
```

For the full run, set `$env:ZmijCache = 'Full'` before the same command and remove it afterward.
