# Shortest-producer size and assembly

This record compares the **finite, nonzero `double` shortest-digit producer** from Zmij with a local `double` specialization of [#131068 at `56ff8516`](../src/UnroundedScaling.Comparison/SOURCE.md), its September 25, 2026 PR head. It does not compare standalone formatters or estimate an incremental `System.Private.CoreLib` change. Source arrays, PE DLL length, and JIT instruction bytes are separate measurements.

## Counted source data

| Source | Elements | Raw bytes |
|---|---:|---:|
| Current .NET Grisu3 cached powers | 87 × (`ulong` + two `short`) | 1,044 |
| Current .NET Grisu3 small powers | 10 × `uint` | 40 |
| **Current Grisu3 total** | Four arrays | **1,084** |
| Zmij compact cached powers | 39 × two `ulong` anchors + 16 × `ulong` minors + 78 fixup bytes | 830 |
| Zmij digit helpers | 20 × `ulong` + 200 ASCII bytes | 360 |
| **Zmij compact total** | Listed arrays | **1,190** |
| Pinned #131068 cached powers | 696 × two `ulong` | 11,136 |
| Pinned #131068 local digit helpers | 64 log bytes + 21 `ulong` powers + 100 `ushort` digit pairs | 432 |
| **Pinned #131068 local total** | Listed arrays in isolated `double` build | **11,568** |

Grisu3 is counted from [`Number.Grisu3.cs` at `dotnet/runtime` `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs) (2026-09-25). Its producer is generic and uses one set of arrays, so the 1,084-byte count is not multiplied by the number of floating-point types. The PR's [`Number.Pow10Table.cs` at `56ff8516`](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.Pow10Table.cs) has 1,392 high/low words; every word was compared with the local table.

The PR's [generic producer](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.UnroundedScaling.cs) shares that table across `double`, `float`, `Half`, and `BFloat16`. **The 16-bit types add no separate cached-power data.** The [PR description](https://github.com/dotnet/runtime/pull/131068) attributes their additional size to native generic specializations. The 432 helper bytes above come from [existing CoreLib routines](../src/UnroundedScaling.Comparison/SOURCE.md), so they are counted in the isolated port but are not new data added by the PR. These counts exclude alignment, metadata, native code, and other formatter data. The port's bounded-precision helper also defines a 160-byte small-power table; that helper is excluded from the shortest-only DLL below.

The optional full Zmij cache stores 618 pairs of `ulong` values (9,888 raw bytes). It is a build alternative used to study cache reconstruction, not a separate row in the compact-versus-#131068 DLL comparison. [`generate_compact_cache.py`](../tools/generate_compact_cache.py) builds the stride-16 tables from integer intervals, and [`verify_compact_cache.py`](../tools/verify_compact_cache.py) checks all 618 reconstructions against exact arithmetic.

## Comparable minimal DLLs

[`ShortestCoreSize.csproj`](../tools/ShortestCoreSize/ShortestCoreSize.csproj) builds the same `Shortest.Core.dll` project twice. Each profile exposes the same `Digits.TryGetSignificantDigits(double, Span<byte>, out int, out int)` method and accepts finite, nonzero values with a 32-byte destination. Neither profile has a presentation writer or a project reference to the other. By default, the Zmij profile source-links its `double` core, normalized decimal type, compact cache, and digit writer; its `float` code is in a separate file and is excluded. The unrounded profile source-links the pinned PR's shortest algorithm specialized for `double`, its identical power table, a local buffer/digit helper, and the adapter; `SHORTEST_ONLY` excludes bounded-precision code and its small-power table.

Windows x64, .NET SDK `11.0.100-rc.1.26425.128`, Release `net11.0`:

| `Shortest.Core.dll` build | File length |
|---|---:|
| `ShortestCore=Zmij` | **12,288 B** |
| `ShortestCore=Unrounded` (pinned #131068 local port) | **19,968 B** |

Both assemblies are built by the same project settings and differ by **7,680 B** as PE files. The comparison is meaningful for these two isolated `double` shortest producers and their identical public adapter. The #131068 row is a local specialization of the pinned generic source with byte-specialized CoreLib helpers; it is not the PR's CoreLib image delta or a result for other types or bounded precision. PE section rounding, metadata, and IL are included in the file lengths.

The [check program](../tools/ShortestCoreSize.Check/Program.cs) ran both compact Zmij and #131068 builds over the same 250,000 deterministic raw patterns. Each accepted 249,884 finite nonzero values, round-tripped them, and produced the same digits-and-scale SHA-256 digest. Run the sequential build and check with `./tools/measure-shortest-core.ps1`.

## Full-cache diagnostic

The same Zmij minimal project can source-link either cache, with the same finite nonzero `double` adapter. Both profiles produced the same digits-and-scale SHA-256 digest over 249,884 inputs. Windows x64, Release `net11.0`:

| Measure | Compact cache | Full cache |
|---|---:|---:|
| Cached-power source constants | 830 B | 9,888 B |
| Minimal `Shortest.Core.dll` | 12,288 B | 20,480 B |
| JIT `ToDecimal(ulong, int, int, ulong, ulong)` | 486 B | 486 B |
| Listed shortest call tree | 1,532 B | 1,355 B |

The [earlier alternating decomposition ShortRuns](benchmark-sessions/cache-profiles-decomposition.md) used the former stride-28 cache: full took **6.118–6.148 ns/value** for varied long significands and **7.460–7.561 ns/value** for random bits; compact took **8.924** and **11.502 ns/value**. The [stride-16 pre-caller runs](benchmark-sessions/stacked-cache-short.md) and [current caller-placement comparison](benchmark-sessions/shared-caller-short.md) are recorded separately. The full table saves reconstruction work but adds **9,058 source-data bytes** and **8,192 B** to the minimal DLL. The measurements cover local canonical decomposition, not a separately timed cache lookup or a CoreLib build.

The outer conversion entry now computes the decimal exponent, shift, and cached power once before calling its regular/irregular rounding method. On the regular path, the compact cache splits the power index into a 16-entry block, reads a minor and two anchor words, performs two 64-bit products, then normalizes and corrects the reconstructed pair. The full profile reads the pair directly. Regular conversion then performs two 64-bit products for scaling and one for the extra digit, followed by rounding and the entry point's trailing-decimal-zero loop. For normal powers of two, scaling uses shifts instead of the two products. The [caller-placement ShortRuns](benchmark-sessions/shared-caller-short.md) measure the effect of moving the shared setup; they do not assign an exact nanosecond count to each operation.

Rebuild and check the full minimal profile with `dotnet build -c Release tools/ShortestCoreSize/ShortestCoreSize.csproj -t:Rebuild -p:ShortestCore=Zmij -p:ZmijCache=Full`, then `dotnet run -c Release --project tools/ShortestCoreSize.Check -p:ShortestCore=Zmij -p:ZmijCache=Full`. Omit `-p:ZmijCache=Full` to return to the compact profile.

## JIT assembly for the same boundary

The check program also drives x64 RyuJIT `FullOpts` with tiering disabled. The public adapter is marked `NoInlining` in both profiles so its call tree appears in the listing. The following are native instruction bytes for methods exercised by that adapter, including its inlined callees:

| Method role | Zmij compact | Pinned #131068 local port |
|---|---:|---:|
| Public adapter | 171 B | 246 B |
| Decode input and prepare scaling | `ToDecimal(double)` 576 B | `ExtractFractionAndBiasedExponent` 64 B |
| Scale and round | `ToDecimal(ulong, int, int, ulong, ulong)` 486 B | `ShortFloat` 708 B |
| Digit writing | `TryGetSignificantDigits(ZmijDecimal, …)` 299 B | `StoreDigits` 373 B, including inlined CoreLib helpers |
| **Listed call-tree total** | **1,532 B** | **1,391 B** |

The compact Zmij path spends instructions reconstructing cached powers: it indexes 16 minors and 39 anchors, multiplies the words, normalizes the result, and applies one correction bit. The pinned unrounded-scaling port reads adjacent table words. Native code is larger for Zmij in this JIT run while its counted data and DLL are smaller. Moving shared scaling setup to the caller changed the Zmij split between the entry and rounding method; the listed total fell from 1,742 to 1,532 bytes. The entry has one call to the rounding method and no additional scaling helper call. These method totals are observations from one architecture and JIT; they cannot be added to the DLL lengths or used as a ReadyToRun estimate.

To capture the same listings after building each profile:

```powershell
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_JitDisasm = 'Shortest.Core!*'
dotnet run -c Release --project tools/ShortestCoreSize.Check -p:ShortestCore=Zmij *> zmij-disasm.txt
dotnet run -c Release --project tools/ShortestCoreSize.Check -p:ShortestCore=Unrounded *> unrounded-disasm.txt
Remove-Item Env:DOTNET_TieredCompilation, Env:DOTNET_JitDisasm
rg 'Assembly listing for method|Total bytes of code' zmij-disasm.txt unrounded-disasm.txt
```

The log files are local outputs. Use [benchmark results](benchmark-results.md) for timings; source-data and code-size counts alone do not predict throughput.

Earlier normalization and result simplifications reduced the Zmij entry method, and the stride-16 cache increased the minimal DLL by 512 B. The later [caller-side move](optimization-notes.md#caller-side-scaling-setup) redistributes code between the entry and private method and reduces their listed total. The posted issue retains the measurements from its pinned earlier revision.
