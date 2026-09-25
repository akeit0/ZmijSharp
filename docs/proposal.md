# Proposal: evaluate a compact Żmij-style shortest producer for `float` and `double`

## Request

Would a focused alternative for the **shortest decimal digit production** step be worth evaluating in .NET's floating-point formatter? The candidate is a fixed-width Żmij-style converter with a compact cached-power table. It would handle finite `float` and `double` values when the requested precision is unconstrained, produce digits and scale, and then feed the existing CoreLib number buffer and presentation path. The proposal adds no public API.

The question is about a measured runtime experiment, not adopting this repository's standalone formatter as-is. Counted precision, other format requests, culture-dependent presentation, and special values would continue through their existing paths.

## Why consider it

Shortest formatting is used by default `ToString`/`TryFormat` calls and by consumers that write floating-point values as text. This repository implements and tests a different shortest producer, so it can help answer whether its conversion cost and table-size tradeoff are useful in CoreLib.

The compact cache reconstructs 618 cached powers from **670 bytes of constants**. For a narrower DLL comparison, the same source-linked shortest-`double` project builds either the Zmij or the older local unrounded-scaling producer with an identical digits-and-scale API and no presentation layer. The Release DLLs measure **11,776 B** and **18,944 B** respectively; the listed x64 JIT call trees measure **2,087 B** and **1,273 B**. This exposes a data-versus-instruction tradeoff in two isolated snapshots, not an incremental CoreLib result or a comparison with the current #131068 head. Current .NET Grisu3 has **1,084 bytes** across its four source tables, counted at [`dotnet/runtime` `f0df4333`](https://github.com/dotnet/runtime/blob/f0df4333553c63a6bdba26228ab94b99e4a1c8d1/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs); the algorithms and table scopes differ.

## Evidence available today

- The default invariant `char` output matched the installed .NET 11 RC runtime for **all 2³² binary32 encodings**. A deterministic **100-million-pattern binary64** sample matched its default `char` and UTF-8 output.
- A separate exact-integer shortest-decimal oracle passed sampled and exponent-boundary cases. A pinned upstream Żmij differential passed one million random patterns per type plus exponent boundaries after normalizing trailing-zero representation differences.
- A matrix of four number-format providers, 14 format strings, and destination capacities passed against the installed runtime. This checks the standalone wrapper; it does not establish CoreLib integration behavior.
- One current-source, same-session Windows x64 ShortRun measured complete `double` `TryFormat` at **30.22 ns** for Zmij versus **31.02 ns** for the installed runtime on a JSON-like corpus, and **45.38 ns** versus **90.41 ns** on uniform raw IEEE bits. Both paths allocated zero bytes. These are three-iteration, standalone measurements; the raw-bit corpus is not an application-average workload.

The output sweep establishes compatibility with one runtime version. Independent shortestness coverage is smaller, and the performance measurements do not isolate the digit producer from the standalone presentation code. A runtime change would need matched CoreLib builds and a broader performance record.

## Relationship to #131068

[dotnet/runtime#131068](https://github.com/dotnet/runtime/pull/131068) proposes unrounded scaling for `Half`, `BFloat16`, `float`, and `double`, including bounded significant digits. At its September 25, 2026 draft head, it already has CoreLib integration and reports matched-runtime tests. Its four types share one cached-power table; the 16-bit types do not add another table. This candidate is narrower: an alternative shortest producer for `float` and `double`, with a compact-cache option.

The local unrounded-scaling comparison uses an older #131068 snapshot (`50ef2d06`), so its local timings are **not** a comparison with the current PR. The decision should compare matched builds of current `main`, #131068, and this candidate, or use #131068 as baseline if it lands first. A direct destination writer can be considered separately if it improves complete formatting; the first standalone UTF-8 variant tried here showed no gain and was removed.

## Feedback requested

- Is this narrower shortest-producer experiment useful alongside or after #131068?
- Is digits plus scale into CoreLib's existing number buffer the right first integration boundary?
- What correctness, performance, portability, and size evidence would make a follow-up PR worth reviewing?
