# `ToDecimal` path-splitting trials

Windows 11 x64, .NET SDK `11.0.100-rc.1.26425.128`, BenchmarkDotNet 0.14.0 ShortRun. Each session used one launch, three warmups, three measured iterations, and 10,000 finite nonzero `double` values per invocation. Setup checked equal canonical tuples against the pinned #131068 local port. Values are ns/value means for `ZmijCanonical`, with no managed allocations reported.

| Source shape and cache | LongSignificand | Random | Simple |
|---|---:|---:|---:|
| Existing combined method, compact, paired session | 8.924 | 11.502 | 19.310 |
| Direct regular/irregular split, compact, ternary dispatch | 8.401 | 12.592 | 18.820 |
| Direct regular/irregular split, compact, regular-first `if` | 8.540 | 12.718 | 19.403 |
| Shared scaling in entry, separate rounding tails, compact, session 1 | 9.088 | 11.443 | 19.320 |
| Shared scaling in entry, separate rounding tails, compact, session 2 | 9.093 | 11.063 | 19.749 |
| Existing combined method, full table, session 1 | 6.118 | 7.460 | 15.942 |
| Existing combined method, full table, session 2 | 6.148 | 7.561 | 15.637 |
| Shared scaling in entry, separate rounding tails, full table, session 1 | 6.441 | 8.509 | 15.963 |
| Shared scaling in entry, separate rounding tails, full table, session 2 | 6.294 | 8.515 | 16.166 |

The direct split moved the regular call to the outer entry, reduced that method to 524 x64 native bytes and its stack reservation from 144 to 80 bytes, but increased the minimal compact DLL from 11,776 to 12,288 B. The shared-before-call version preserved the 11,776 B compact DLL and reduced the regular call-path code, yet its full-table random timing was consistently about 1 ns/value slower. Both trials passed the 249,884-input output digest and a 2,000,000-pattern plus exponent-boundary agreement check. Neither source shape was retained.

Reproduce the current baseline with `dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short --filter "*DecompositionBenchmarks*"`; set `$env:ZmijCache = 'Full'` before that command for the full-table profile. The experimental shapes were temporary source changes and are summarized here rather than kept as build modes.
