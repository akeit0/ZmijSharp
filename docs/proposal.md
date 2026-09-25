# Proposal: evaluate a compact Żmij-style shortest producer alongside #131068 and Ryu

## Request

Would a focused alternative for the **shortest decimal digit production** step be worth evaluating in .NET's floating-point formatter? The candidate is a fixed-width Żmij-style converter with a compact cached-power table. It would handle finite `float` and `double` values when the requested precision is unconstrained, produce digits and scale, and then feed the existing CoreLib number buffer and presentation path. The proposal adds no public API.

The question is about a measured runtime experiment, not adopting this repository's standalone formatter as-is. Counted precision, other format requests, culture-dependent presentation, and special values would continue through their existing paths.

## Why consider it

Shortest formatting is used by default `ToString`/`TryFormat` calls and by consumers that write floating-point values as text. This repository implements and tests a different shortest producer, so it can help answer whether its conversion cost and table-size tradeoff are useful in CoreLib.

The compact cache reconstructs 618 cached powers. These are raw source-data counts at pinned revisions, with each implementation's existing scope:

| Source data | Bytes | Scope |
|---|---:|---|
| Zmij compact power cache | 670 | Shared by this repository's `float` and `double` shortest producers |
| Zmij digit helpers | 360 | Digit-writing constants; 1,030 B together with the cache |
| [Grisu3 tables at `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs) | 1,084 | Four source arrays shared by the existing producer |
| [#131068 power cache at `56ff8516`](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.Pow10Table.cs) | 11,136 | One table shared across four types and bounded precision |

The isolated, presentation-free Zmij shortest-`double` project builds to an **11,776 B** Release DLL; its listed x64 JIT call tree is **2,087 instruction bytes**. These are separate measurements, not an incremental CoreLib size estimate. The source-data rows also have different algorithm and type scopes. See the [size and assembly record](https://github.com/akeit0/ZmijSharp/blob/f8934e272bf71e579cf79492db011cd3aac02c12/docs/size-and-assembly.md) for the counted arrays and JIT method list.

## Evidence available today

- The default invariant `char` output matched the installed .NET 11 RC runtime for **all 2³² binary32 encodings**. A deterministic **100-million-pattern binary64** sample matched its default `char` and UTF-8 output.
- A separate exact-integer shortest-decimal oracle passed sampled and exponent-boundary cases. A pinned upstream Żmij differential passed one million random patterns per type plus exponent boundaries after normalizing trailing-zero representation differences.
- A matrix of four number-format providers, 14 format strings, and destination capacities passed against the installed runtime. This checks the standalone wrapper; it does not establish CoreLib integration behavior.

One same-session Windows x64 ShortRun measured complete `double` `TryFormat` into preallocated `Span<char>` buffers. Each result is nanoseconds per value over a deterministic 10,000-value corpus; lower is better. The ratio is Zmij divided by the installed runtime.

| Corpus | Runtime `TryFormat` | Zmij `TryFormat` | Ratio |
|---|---:|---:|---:|
| Simple | 33.68 ns | 27.97 ns | 0.83 |
| JsonLike | 31.02 ns | 30.22 ns | 0.97 |
| LongSignificand | 65.92 ns | 24.51 ns | 0.37 |
| Random raw IEEE bits | 90.41 ns | 45.38 ns | 0.50 |
| Extreme | 50.03 ns | 29.23 ns | 0.58 |

This BenchmarkDotNet 0.14.0 ShortRun used the .NET 11 RC runtime on one Windows x64 machine, one launch, three warmup iterations, and three measured iterations. All rows recorded zero allocations. These are standalone formatting measurements; they do not isolate the digit producer, predict a CoreLib result, or represent an average application's value mix. The [full methods and results](https://github.com/akeit0/ZmijSharp/blob/f8934e272bf71e579cf79492db011cd3aac02c12/docs/benchmark-results.md) include digits-only measurements.

The output sweep establishes compatibility with one runtime version. Independent shortestness coverage is smaller. A runtime change would need matched CoreLib builds and a broader performance record.

## Relationship to #131068 and #134621

[dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068) proposes unrounded scaling for `Half`, `BFloat16`, `float`, and `double`, including bounded significant digits. At its September 25, 2026 draft head, it already has CoreLib integration and reports matched-runtime tests. Its four types share one cached-power table; the 16-bit types do not add another table. [Issue #134621](https://github.com/dotnet/runtime/issues/134621) proposes a Ryu-based shortest `double` replacement and reports matched CoreLib benchmarks. [Maintainer feedback there](https://github.com/dotnet/runtime/issues/134621#issuecomment-5823161017) asks for comparison against #131068 and raises algorithm-count, type-coverage, and broader-formatting concerns.

This candidate covers shortest `float` and `double`; it does not yet provide `Half`, `BFloat16`, or bounded precision. The benchmark table compares only Zmij with the installed runtime. It cannot rank Żmij against #131068 or the Ryu prototype: the builds, machines, and corpora differ. The decision should compare matched CoreLib builds of current `main`, #131068, Ryu, and this candidate, or use #131068 as baseline if it lands first. Algorithm count and maintenance cost belong in that decision.

## Feedback requested

- Is this narrower shortest-producer experiment useful given #131068 and #134621?
- Is digits plus scale into CoreLib's existing number buffer the right first integration boundary?
- Would an alternative need `Half`, `BFloat16`, and bounded precision before consideration?
- What correctness, performance, portability, and size evidence would make a follow-up PR worth reviewing?

## Reproducible reference

The [public ZmijSharp revision `f8934e2`](https://github.com/akeit0/ZmijSharp/tree/f8934e272bf71e579cf79492db011cd3aac02c12) contains the implementation used for these local measurements, the [completed evidence record](https://github.com/akeit0/ZmijSharp/blob/f8934e272bf71e579cf79492db011cd3aac02c12/docs/evidence.md), the [benchmark method and results](https://github.com/akeit0/ZmijSharp/blob/f8934e272bf71e579cf79492db011cd3aac02c12/docs/benchmark-results.md), and the [size and assembly record](https://github.com/akeit0/ZmijSharp/blob/f8934e272bf71e579cf79492db011cd3aac02c12/docs/size-and-assembly.md). The current issue draft is maintained in this repository; the evidence links point to an immutable revision.
