# ZmijSharp

ZmijSharp is an experimental C# implementation of shortest decimal formatting for IEEE 754 `float` and `double`. It explores whether a fixed-width Żmij/Schubfach-style converter and a compact power-of-ten cache could improve .NET's formatting path. This repository is a test bed for a possible [`dotnet/runtime` proposal](docs/proposal.md), not a runtime patch or a new .NET API proposal.

The work overlaps with [dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068), which proposes unrounded scaling for floating-point formatting, and [the Ryu proposal #134621](https://github.com/dotnet/runtime/issues/134621). The issue proposal focuses on an alternative shortest digit producer. The local comparison project uses a [pinned `double` specialization](src/UnroundedScaling.Comparison/SOURCE.md) of #131068 at `56ff8516`; it is not a matched CoreLib build or a measurement of later PR revisions.

## Proposal and work plan

| Read this | Purpose |
|---|---|
| [Proposal](docs/proposal.md) | The argument and questions intended for a `dotnet/runtime` issue |
| [Completed evidence](docs/evidence.md) | Checks that ran, what they establish, and how to repeat them |
| [Benchmark results](docs/benchmark-results.md) and [size analysis](docs/size-and-assembly.md) | Measurement details behind the proposal |
| [Work plan](docs/plan.md) | Remaining repository tasks, publication steps, and a possible runtime experiment |

The proposal describes **what to show**; the plan records **what to do next**. No issue has been posted.

## What is in the repository

| Project | Purpose |
|---|---|
| [`src/ZmijSharp`](src/ZmijSharp) | Shortest converter, compact cache, and standalone `char`/invariant UTF-8 formatter for `float` and `double` |
| [`src/UnroundedScaling.Comparison`](src/UnroundedScaling.Comparison) | `double`-only port of #131068 at `56ff8516`, with the same local presentation code for complete-path comparisons |
| [`src/ZmijSharp.RuntimeShim`](src/ZmijSharp.RuntimeShim) | Limited runtime-shaped `double`/UTF-8 path for measuring conversion and formatting costs |
| [`tests/ZmijSharp.Tests`](tests/ZmijSharp.Tests) | TUnit behavior and destination-buffer tests |
| [`tests/ZmijSharp.Verify`](tests/ZmijSharp.Verify) | Runtime differential checks, independent `BigInteger` oracle, and long producer sweeps |
| [`benchmarks/ZmijSharp.Benchmarks`](benchmarks/ZmijSharp.Benchmarks) | BenchmarkDotNet comparisons on deterministic input corpora |
| [`tools`](tools) | Cache derivation, JIT disassembly, and isolated shortest-producer size checks |

The optimized path covers default formatting, `G/g`, `G0/g0`, and `R/r` (including accepted `R` precision). Explicit `G` precision and other formats delegate to the installed .NET runtime. `TryFormatUtf8` uses invariant formatting; `char` formatting accepts an `IFormatProvider`. `ToDecimal` accepts finite values only. The [work plan](docs/plan.md) states the proposed runtime boundary.

## Current evidence

- All 2³² binary32 patterns passed default invariant `char` output comparison with the installed runtime. Sampled and structured inputs also pass an independent shortest-decimal oracle.
- A deterministic 100-million-pattern `double` sample passed default `char` and UTF-8 output comparison. A pinned upstream Żmij differential passed on one million random patterns per type plus exponent boundaries after trailing-zero normalization.
- In two local complete-path benchmark sessions, Zmij led the pinned #131068 `double` port on raw-bit and long-significand inputs; the port led on JSON-like values. Simple values changed order between sessions. These standalone results are not a comparison on identical CoreLib builds.
- ARM64, ReadyToRun, NativeAOT, and CoreLib integration have not been measured here.

For exact commands and coverage, see [completed evidence](docs/evidence.md). For methods, environment, and numbers, see [benchmark results](docs/benchmark-results.md). The [work plan](docs/plan.md) tracks what is still needed.

## Build and run

Install the SDK version pinned in [`global.json`](global.json) (currently .NET 11 RC 1), then run from the repository root:

```bash
dotnet build -c Release ZmijSharp.slnx
dotnet test -c Release --project tests/ZmijSharp.Tests
dotnet run -c Release --project tests/ZmijSharp.Verify
```

The default verification checks targeted boundaries, 100,000 random patterns of each type, and a smaller independent exact oracle sample. For exhaustive binary32 output comparison, run each of four shards separately; this example runs shard 0:

```bash
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --float-output-exhaustive 0 4
```

The full sweep requires shards 0 through 3. It compares default invariant `char` output with this machine's runtime; it does not independently prove shortestness for all 2³² values. The [evidence record](docs/evidence.md) lists the larger `double`, culture, cache, and upstream differential commands.

Run the benchmarks with:

```bash
dotnet run -c Release --project benchmarks/ZmijSharp.Benchmarks -- --job Short
```

Benchmark methods include runtime, Zmij, comparison-port, and runtime-shim paths. Compare rows from the same session and the same scope (complete formatting or digits only). The [recorded results](docs/benchmark-results.md) explain each corpus and comparison.

## API example

```csharp
using System.Globalization;
using ZmijSharp;

string text = ZmijFormatter.Format(Math.PI, "R", CultureInfo.InvariantCulture);

Span<char> chars = stackalloc char[32];
bool charSuccess = ZmijFormatter.TryFormat(
    Math.PI, chars, out int charsWritten, "G", CultureInfo.InvariantCulture);

Span<byte> utf8 = stackalloc byte[32];
bool utf8Success = ZmijFormatter.TryFormatUtf8(
    Math.PI, utf8, out int bytesWritten, "G");

ZmijDecimal decimalValue = ZmijFormatter.ToDecimal(Math.PI);
// decimalValue.Significand * 10^decimalValue.Exponent, with IsNegative separately.
```

The original work in this repository is [MIT licensed](LICENSE) under Akito Inoue's name. [Third-party notices](THIRD-PARTY-NOTICES.txt) retain the rights and licenses of the projects used as references or sources. Żmij and Zmij.NET were references for the converter; the available history does not establish an exact source-derived portion.
