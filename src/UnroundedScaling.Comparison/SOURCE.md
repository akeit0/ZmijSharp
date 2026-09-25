# UnroundedScaling source

`UnroundedScaling.Comparison` is a standalone comparison implementation of the unrounded-scaling shortest-decimal algorithm discussed in [dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068).

The adapted source was taken from the PR contributor branch snapshot [`50ef2d06d59f83760206ebcf59268dccdce45e1d`](https://github.com/PranavSenthilnathan/runtime/commit/50ef2d06d59f83760206ebcf59268dccdce45e1d), specifically `Number.UnroundedScaling.cs` and `Number.Pow10Table.cs`.

The adapted producer does not call `System.Private.CoreLib`, `NumberBuffer`, `Number.UnroundedScaling`, or other CoreLib formatting internals. Its complete-format comparison wrapper does reference `ZmijSharp` for presentation, so the project DLL is not a standalone producer-size measurement. The [minimal size project](../../tools/ShortestCoreSize/ShortestCoreSize.csproj) compiles the producer without that wrapper or project reference and excludes bounded precision with `SHORTEST_ONLY`.

The upstream .NET Foundation/MIT headers and Go BSD attribution are retained in the adapted source files. Any redistribution must preserve those notices.

## Comparison harness (this repository's code, not upstream)

- `UnroundedScalingDigits.TryGetSignificantDigits` adapts `TryRun` to the
  same digits+scale shape as `ZmijCore.TryGetSignificantDigits` (sign is
  normalized away inside `TryRun`). Used by the significant-digits
  diagnostic benches and the Verify agreement audit.
- `UnroundedScalingFormatter` provides the shortest unconstrained `char` and
  invariant UTF-8 formatting paths over the same decomposition, reusing
  `ZmijFormatter` presentation (`TryWriteChar`/`TryWriteUtf8`) verbatim so
  formatted comparisons isolate decomposition cost. Counted precision,
  non-finite values, and unsupported formats fall back to the runtime public
  API. It calls `TryRun` directly (finite non-zero input is already
  established); the `char` path widens ASCII digits with a tight loop while
  the UTF-8 path feeds them direct.
