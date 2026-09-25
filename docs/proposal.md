# Proposal: evaluate a compact Żmij-style shortest producer alongside #131068 and Ryu

## Request

Would a focused alternative for the **shortest decimal digit production** step be worth evaluating in .NET's floating-point formatter? The candidate is a fixed-width Żmij-style converter with a compact cached-power table. It would handle finite `float` and `double` values when the requested precision is unconstrained, produce digits and scale, and then feed the existing CoreLib number buffer and presentation path. The proposal adds no public API.

The question is about a measured runtime experiment, not adopting this repository's standalone formatter as-is. Counted precision, other format requests, culture-dependent presentation, and special values would continue through their existing paths.

## Why consider it

Shortest formatting is used by default `ToString`/`TryFormat` calls and by consumers that write floating-point values as text. This repository implements and tests a different shortest producer, so it can help answer whether its conversion cost and table-size tradeoff are useful in CoreLib.

The compact cache reconstructs 618 cached powers. The same minimal, presentation-free project builds each shortest-`double` producer behind an identical digits-and-scale API. Its #131068 profile specializes [the generic producer at `56ff8516`](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.UnroundedScaling.cs) for `double` and byte-specializes the pinned CoreLib digit helpers.

| Size measure | Zmij compact | #131068 local `double` port |
|---|---:|---:|
| Power-cache source constants | 830 B | 11,136 B |
| Minimal Release `Shortest.Core.dll` | 12,288 B | 19,968 B |
| Listed x64 JIT call tree | 1,532 B | 1,391 B |

The #131068 [power table](https://github.com/dotnet/runtime/blob/56ff851680b3c64a9ecaf543225b9cc948fe0262/src/libraries/System.Private.CoreLib/src/System/Number.Pow10Table.cs) is shared across four types and bounded precision; the local DLL and JIT rows cover only its `double` shortest path. The DLL figures are PE file lengths, while the JIT figures are native instruction bytes. None is an incremental CoreLib size estimate. The Zmij JIT row includes [later code-size reductions](optimization-notes.md). For separate context, Grisu3 has **1,084 bytes** across its four source tables at [`dotnet/runtime` `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs). See the size and assembly record linked below for the counted arrays and method list.

The [full-cache diagnostic](size-and-assembly.md#full-cache-diagnostic) makes the cache tradeoff visible. The current minimal Zmij DLLs are **12,288 B** compact and **20,480 B** full. The full profile uses **9,888 B** of cached-power source constants versus **830 B** compact. The performance table below compares both current profiles with the pinned #131068 local producer. These are local measurements, not matched CoreLib results. The posted issue retains its pinned earlier measurements.

## Evidence available today

- The default invariant `char` output matched the installed .NET 11 RC runtime for **all 2³² binary32 encodings**. A deterministic **100-million-pattern binary64** sample matched its default `char` and UTF-8 output.
- A separate exact-integer shortest-decimal oracle passed sampled and exponent-boundary cases. A pinned upstream Żmij differential passed one million random patterns per type plus exponent boundaries after normalizing trailing-zero representation differences.
- A matrix of four number-format providers, 14 format strings, and destination capacities passed against the installed runtime. This checks the standalone wrapper; it does not establish CoreLib integration behavior.

The [current decomposition ShortRuns](benchmark-sessions/current-cache-comparison-short.md) measured finite nonzero `double` conversion without digit writing on Windows x64 with .NET 11 RC. Each cache profile and its #131068 control ran in the same BenchmarkDotNet launch; the compact and full launches were separate. Both local producers returned the same canonical `(significand, exponent)` on every input. Values are ns/value over 10,000-value corpora; lower is better.

| Corpus | Zmij compact | UnroundedScaling, compact launch | Zmij full | UnroundedScaling, full launch |
|---|---:|---:|---:|---:|
| Simple | 19.033 | 15.524 | 15.493 | 15.161 |
| Varied long significands | 8.510 | 6.585 | 6.177 | 6.632 |
| Random raw IEEE bits | 10.915 | 8.934 | 6.760 | 8.764 |

The compact profile was slower than the local #131068 port on these corpora. In one full-cache launch, Zmij was faster on varied long significands and random bits and close on simple values, at the larger table and DLL size above. The port's diagnostic canonical helper trims integer trailing zeros, whereas production `TryRun` trims written bytes; Zmij's entry point retains sign and exceptional-value handling. The buffer probe found pointer and span storage near parity, while byte-to-`char` staging in the local comparison wrapper had a larger measured cost. These component results do not predict relative performance in CoreLib; [methods, caveats, and raw summaries](component-benchmarks.md) are linked here.

The output sweep establishes compatibility with one runtime version. Independent shortestness coverage is smaller. A runtime change would need matched CoreLib builds and a broader performance record.

## Relationship to #131068 and #134621

[dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068) proposes unrounded scaling for `Half`, `BFloat16`, `float`, and `double`, including bounded significant digits. At its September 25, 2026 draft head, it already has CoreLib integration and reports matched-runtime tests. Its four types share one cached-power table; the 16-bit types do not add another table. [Issue #134621](https://github.com/dotnet/runtime/issues/134621) proposes a Ryu-based shortest `double` replacement and reports matched CoreLib benchmarks. [Maintainer feedback there](https://github.com/dotnet/runtime/issues/134621#issuecomment-5823161017) asks for comparison against #131068 and raises algorithm-count, type-coverage, and broader-formatting concerns.

This candidate covers shortest `float` and `double`; it does not yet provide `Half`, `BFloat16`, or bounded precision. The component and size tables compare Zmij with a local `double` port of the pinned #131068 algorithm. They do not measure the PR's generic CoreLib implementation or its other types and formats. They also cannot rank Żmij against the Ryu prototype. The decision should compare matched CoreLib builds of current `main`, #131068, Ryu, and this candidate, or use #131068 as baseline if it lands first. Algorithm count and maintenance cost belong in that decision.

## Feedback requested

- Is this narrower shortest-producer experiment useful given #131068 and #134621?
- Is digits plus scale into CoreLib's existing number buffer the right first integration boundary?
- Would an alternative need `Half`, `BFloat16`, and bounded precision before consideration?
- What correctness, performance, portability, and size evidence would make a follow-up PR worth reviewing?

## Reproducible reference

The [public ZmijSharp revision `e929052`](https://github.com/akeit0/ZmijSharp/tree/e92905250febc735d5e253b4260e23e14123e83b) contains the [component methods, measurements, and raw summaries](https://github.com/akeit0/ZmijSharp/blob/e92905250febc735d5e253b4260e23e14123e83b/docs/component-benchmarks.md). The [size and assembly record](https://github.com/akeit0/ZmijSharp/blob/b051752a56215349ba47ead4aa64dd6b9a154102/docs/size-and-assembly.md) is pinned to the original minimal-project measurement. These links point to immutable revisions.
