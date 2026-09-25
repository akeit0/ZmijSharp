# Proposal: evaluate a compact Żmij-style shortest producer alongside #131068 and Ryu

## Request

Would a focused alternative for the **shortest decimal digit production** step be worth evaluating in .NET's floating-point formatter? The candidate is a fixed-width Żmij-style converter with a compact cached-power table. It would handle finite `float` and `double` values when the requested precision is unconstrained, produce digits and scale, and then feed the existing CoreLib number buffer and presentation path. The proposal adds no public API.

The question is about a measured runtime experiment, not adopting this repository's standalone formatter as-is. Counted precision, other format requests, culture-dependent presentation, and special values would continue through their existing paths.

## Why consider it

Shortest formatting is used by default `ToString`/`TryFormat` calls and by consumers that write floating-point values as text. This repository implements and tests a different shortest producer, so it can help answer whether its conversion cost and table-size tradeoff are useful in CoreLib.

The compact cache reconstructs 618 cached powers. The same minimal, presentation-free project builds each shortest-`double` producer behind an identical digits-and-scale API. Its #131068 profile specializes [the generic producer at `56ff8516`](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.UnroundedScaling.cs) for `double` and byte-specializes the pinned CoreLib digit helpers.

| Size measure | Zmij compact | #131068 local `double` port |
|---|---:|---:|
| Power-cache source constants | 670 B | 11,136 B |
| Minimal Release `Shortest.Core.dll` | 11,776 B | 19,968 B |
| Listed x64 JIT call tree | 2,087 B | 1,391 B |

The #131068 [power table](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.Pow10Table.cs) is shared across four types and bounded precision; the local DLL and JIT rows cover only its `double` shortest path. The DLL figures are PE file lengths, while the JIT figures are native instruction bytes. None is an incremental CoreLib size estimate. For separate context, Grisu3 has **1,084 bytes** across its four source tables at [`dotnet/runtime` `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs). See the size and assembly record linked below for the counted arrays and method list.

## Evidence available today

- The default invariant `char` output matched the installed .NET 11 RC runtime for **all 2³² binary32 encodings**. A deterministic **100-million-pattern binary64** sample matched its default `char` and UTF-8 output.
- A separate exact-integer shortest-decimal oracle passed sampled and exponent-boundary cases. A pinned upstream Żmij differential passed one million random patterns per type plus exponent boundaries after normalizing trailing-zero representation differences.
- A matrix of four number-format providers, 14 format strings, and destination capacities passed against the installed runtime. This checks the standalone wrapper; it does not establish CoreLib integration behavior.

Two Windows x64 ShortRun sessions measured complete `double` `TryFormat` into preallocated `Span<char>` buffers. Each range gives the two session means in nanoseconds per value over a deterministic 10,000-value corpus; lower is better. The #131068 column uses the local `double` port pinned to `56ff8516`. The local implementations share fixed/scientific presentation code but stage and write digits differently; the installed runtime uses its own formatter.

| Corpus | Runtime | #131068 local port | Zmij |
|---|---:|---:|---:|
| Simple | 33.25–34.77 ns | 27.41–30.34 ns | 27.95–30.17 ns |
| JsonLike | 30.36–30.61 ns | 28.08–28.68 ns | 29.93–30.10 ns |
| LongSignificand | 64.91–66.31 ns | 33.57–39.79 ns | 24.40–24.68 ns |
| Random raw IEEE bits | 88.58–89.46 ns | 49.82–51.38 ns | 44.72–45.48 ns |
| Extreme | 49.03–49.51 ns | 29.83–30.89 ns | 29.08–29.18 ns |

The #131068 port led on `JsonLike` in both sessions; Zmij led on `LongSignificand` and raw-bit inputs in both. `Simple` changed order, and `Extreme` showed a small Zmij lead. Digits-only measurements favor the #131068 port on most corpora; those methods omit complete formatting and their zero handling differs. BenchmarkDotNet 0.14.0 ShortRun used the .NET 11 RC runtime on one Windows x64 machine, one launch per session, three warmup iterations, and three measured iterations. All rows recorded zero allocations. These standalone measurements do not predict a CoreLib result or represent an average application's value mix. The full methods, results, and raw sessions are linked below.

The output sweep establishes compatibility with one runtime version. Independent shortestness coverage is smaller. A runtime change would need matched CoreLib builds and a broader performance record.

## Relationship to #131068 and #134621

[dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068) proposes unrounded scaling for `Half`, `BFloat16`, `float`, and `double`, including bounded significant digits. At its September 25, 2026 draft head, it already has CoreLib integration and reports matched-runtime tests. Its four types share one cached-power table; the 16-bit types do not add another table. [Issue #134621](https://github.com/dotnet/runtime/issues/134621) proposes a Ryu-based shortest `double` replacement and reports matched CoreLib benchmarks. [Maintainer feedback there](https://github.com/dotnet/runtime/issues/134621#issuecomment-5823161017) asks for comparison against #131068 and raises algorithm-count, type-coverage, and broader-formatting concerns.

This candidate covers shortest `float` and `double`; it does not yet provide `Half`, `BFloat16`, or bounded precision. The benchmark and size tables compare Zmij with a local `double` port of the pinned #131068 algorithm. They do not measure the PR's generic CoreLib implementation or its other types and formats. They also cannot rank Żmij against the Ryu prototype. The decision should compare matched CoreLib builds of current `main`, #131068, Ryu, and this candidate, or use #131068 as baseline if it lands first. Algorithm count and maintenance cost belong in that decision.

## Feedback requested

- Is this narrower shortest-producer experiment useful given #131068 and #134621?
- Is digits plus scale into CoreLib's existing number buffer the right first integration boundary?
- Would an alternative need `Half`, `BFloat16`, and bounded precision before consideration?
- What correctness, performance, portability, and size evidence would make a follow-up PR worth reviewing?

## Reproducible reference

The [public ZmijSharp revision `b051752`](https://github.com/akeit0/ZmijSharp/tree/b051752a56215349ba47ead4aa64dd6b9a154102) contains the measured implementation, the [completed evidence record](https://github.com/akeit0/ZmijSharp/blob/b051752a56215349ba47ead4aa64dd6b9a154102/docs/evidence.md), the [benchmark method and results](https://github.com/akeit0/ZmijSharp/blob/b051752a56215349ba47ead4aa64dd6b9a154102/docs/benchmark-results.md), the [two raw benchmark sessions](https://github.com/akeit0/ZmijSharp/tree/b051752a56215349ba47ead4aa64dd6b9a154102/docs/benchmark-sessions), and the [size and assembly record](https://github.com/akeit0/ZmijSharp/blob/b051752a56215349ba47ead4aa64dd6b9a154102/docs/size-and-assembly.md). The evidence links point to an immutable revision.
