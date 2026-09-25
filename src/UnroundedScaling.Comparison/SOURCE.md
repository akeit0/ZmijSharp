# UnroundedScaling source

`UnroundedScaling.Comparison` is a standalone comparison implementation of the unrounded-scaling shortest-decimal algorithm discussed in [dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068).

The adapted source is pinned to [`dotnet/runtime` PR head `56ff851680b3c64a9ecaf543225b9cc948fe0262`](https://github.com/dotnet/runtime/commit/56ff851680b3c64a9ecaf543225b9cc948fe0262), specifically `Number.UnroundedScaling.cs` and `Number.Pow10Table.cs`. The upstream producer is generic across four floating-point types; this local comparison specializes its shortest path for `double`. [`PortableTypes.cs`](PortableTypes.cs) byte-specializes the pinned CoreLib `FormattingHelpers.CountDigits`, `UInt64ToDecChars`, and `WriteTwoDigits` helpers and supplies the local buffer shape. The 1,392 cached-power words were checked against the pinned upstream file and are identical. Results describe this pinned implementation, not later PR revisions or a matched CoreLib build.

The adapted producer does not call `System.Private.CoreLib`, `NumberBuffer`, `Number.UnroundedScaling`, or other CoreLib formatting internals. Its complete-format comparison wrapper does reference `ZmijSharp` for presentation, so the project DLL is not a standalone producer-size measurement. The [minimal size project](../../tools/ShortestCoreSize/ShortestCoreSize.csproj) compiles the producer without that wrapper or project reference and excludes bounded precision with `SHORTEST_ONLY`.

The upstream .NET Foundation/MIT headers and Go BSD attribution are retained in the adapted source files. Any redistribution must preserve those notices.

## Comparison harness (this repository's code, not upstream)

- `UnroundedScalingDigits.TryGetSignificantDigits` adapts `TryRun` to the
  same digits+scale shape as `ZmijCore.TryGetSignificantDigits` (sign is
  normalized away inside `TryRun`). Used by the significant-digits
  diagnostic benches and the Verify agreement audit.
- `UnroundedScalingFormatter` provides the shortest unconstrained `char` and
  invariant UTF-8 formatting paths over the same decomposition, reusing
  `ZmijFormatter` presentation (`TryWriteChar`/`TryWriteUtf8`) verbatim. The
  digit-writing steps differ, so complete-format comparisons include those
  costs. Counted precision, non-finite values, and unsupported formats fall
  back to the runtime public API. It calls `TryRun` directly (finite non-zero
  input is already established); the `char` path widens ASCII digits with a
  tight loop while the UTF-8 path feeds them direct.
