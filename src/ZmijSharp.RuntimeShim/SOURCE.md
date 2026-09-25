# RuntimeShim source

`ZmijSharp.RuntimeShim` is a standalone benchmark-support assembly. It ports
only the structure of `Number.Formatting.TryFormatFloat<double,byte>`
(dotnet/runtime commit `e8646e7`, ~L929) — finite-check, format parse, convert,
general-writer, copy-if-fits — with the Zmij producer in the Grisu3/Dragon4
slot, so benchmarks can split the runtime path-shape cost from the conversion
cost. Writer/parser adapted from
`src/libraries/Common/src/System/Number.Formatting.Common.cs`
(`ParseFormatSpecifier`, `NumberToString 'G'`, `FormatGeneral`,
`FormatExponent`), double/byte/invariant shortest scope only.

This project is an independent comparison assembly. It does not reference or
call `System.Private.CoreLib` formatting internals. The upstream .NET
Foundation/MIT headers are retained in the adapted source file. Any
redistribution must preserve those notices.
