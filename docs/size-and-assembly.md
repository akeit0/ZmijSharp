# Shortest-producer size and assembly

This record compares the **finite, nonzero `double` shortest-digit producer** from Zmij with a local `double` specialization of [#131068 at `56ff8516`](../src/UnroundedScaling.Comparison/SOURCE.md), its September 25, 2026 PR head. It does not compare standalone formatters or estimate an incremental `System.Private.CoreLib` change. Source arrays, PE DLL length, and JIT instruction bytes are separate measurements.

## Counted source data

| Source | Elements | Raw bytes |
|---|---:|---:|
| Current .NET Grisu3 cached powers | 87 × (`ulong` + two `short`) | 1,044 |
| Current .NET Grisu3 small powers | 10 × `uint` | 40 |
| **Current Grisu3 total** | Four arrays | **1,084** |
| Zmij compact cached powers | 23 × two `ulong` anchors + 28 × `ulong` minors + 78 fixup bytes | 670 |
| Zmij digit helpers | 20 × `ulong` + 200 ASCII bytes | 360 |
| **Zmij compact total** | Listed arrays | **1,030** |
| Pinned #131068 cached powers | 696 × two `ulong` | 11,136 |
| Pinned #131068 local digit helpers | 64 log bytes + 21 `ulong` powers + 100 `ushort` digit pairs | 432 |
| **Pinned #131068 local total** | Listed arrays in isolated `double` build | **11,568** |

Grisu3 is counted from [`Number.Grisu3.cs` at `dotnet/runtime` `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs) (2026-09-25). Its producer is generic and uses one set of arrays, so the 1,084-byte count is not multiplied by the number of floating-point types. The PR's [`Number.Pow10Table.cs` at `56ff8516`](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.Pow10Table.cs) has 1,392 high/low words; every word was compared with the local table.

The PR's [generic producer](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.UnroundedScaling.cs) shares that table across `double`, `float`, `Half`, and `BFloat16`. **The 16-bit types add no separate cached-power data.** The [PR description](https://github.com/dotnet/runtime/pull/131068) attributes their additional size to native generic specializations. The 432 helper bytes above come from [existing CoreLib routines](../src/UnroundedScaling.Comparison/SOURCE.md), so they are counted in the isolated port but are not new data added by the PR. These counts exclude alignment, metadata, native code, and other formatter data. The port's bounded-precision helper also defines a 160-byte small-power table; that helper is excluded from the shortest-only DLL below.

The optional full Zmij cache stores 618 pairs of `ulong` values (9,888 raw bytes). It is a build alternative used to study cache reconstruction, not a separate row in the DLL comparison. [`verify_compact_cache.py`](../tools/verify_compact_cache.py) checks all 618 compact reconstructions against exact arithmetic.

## Comparable minimal DLLs

[`ShortestCoreSize.csproj`](../tools/ShortestCoreSize/ShortestCoreSize.csproj) builds the same `Shortest.Core.dll` project twice. Each profile exposes the same `Digits.TryGetSignificantDigits(double, Span<byte>, out int, out int)` method and accepts finite, nonzero values with a 32-byte destination. Neither profile has a presentation writer or a project reference to the other. The Zmij profile source-links its `double` core, normalized decimal type, compact cache, and digit writer; its `float` code is in a separate file and is excluded. The unrounded profile source-links the pinned PR's shortest algorithm specialized for `double`, its identical power table, a local buffer/digit helper, and the adapter; `SHORTEST_ONLY` excludes bounded-precision code and its small-power table.

Windows x64, .NET SDK `11.0.100-rc.1.26425.128`, Release `net11.0`:

| `Shortest.Core.dll` build | File length |
|---|---:|
| `ShortestCore=Zmij` | **11,776 B** |
| `ShortestCore=Unrounded` (pinned #131068 local port) | **19,968 B** |

Both assemblies are built by the same project settings and differ by **8,192 B** as PE files. The comparison is meaningful for these two isolated `double` shortest producers and their identical public adapter. The #131068 row is a local specialization of the pinned generic source with byte-specialized CoreLib helpers; it is not the PR's CoreLib image delta or a result for other types or bounded precision. PE section rounding, metadata, and IL are included in the file lengths.

The [check program](../tools/ShortestCoreSize.Check/Program.cs) ran both builds over the same 250,000 deterministic raw patterns. Each accepted 249,884 finite nonzero values, round-tripped them, and produced the same digits-and-scale SHA-256 digest. Run the sequential build and check with:

```powershell
./tools/measure-shortest-core.ps1
```

## JIT assembly for the same boundary

The check program also drives x64 RyuJIT `FullOpts` with tiering disabled. The public adapter is marked `NoInlining` in both profiles so its call tree appears in the listing. The following are native instruction bytes for methods exercised by that adapter, including its inlined callees:

| Method role | Zmij compact | Pinned #131068 local port |
|---|---:|---:|
| Public adapter | 171 B | 246 B |
| Decode input | `ToDecimal(double)` 301 B | `ExtractFractionAndBiasedExponent` 64 B |
| Shortest conversion | `ToDecimal(ulong, int, bool)` 1,097 B | `ShortFloat` 708 B |
| Digit writing | `TryGetSignificantDigits(ZmijDecimal, …)` 299 B | `StoreDigits` 373 B, including inlined CoreLib helpers |
| **Listed call-tree total** | **1,868 B** | **1,391 B** |

The compact Zmij path spends instructions reconstructing cached powers: it indexes 28 minors and 23 anchors, multiplies the words, normalizes the result, and applies one correction bit. The pinned unrounded-scaling port reads adjacent table words. Native code is larger for Zmij in this JIT run while its counted data and DLL are smaller. These method totals are observations from one architecture and JIT; they cannot be added to the DLL lengths or used as a ReadyToRun estimate.

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

The smaller Zmij entry method comes from [consolidating finite normalization paths](optimization-notes.md); the minimal DLL length is unchanged. The issue draft's earlier JIT figure is pinned to its measured revision.
