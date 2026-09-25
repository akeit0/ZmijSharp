# Work plan

The [proposal](proposal.md) is the text to show `dotnet/runtime` maintainers. The [evidence record](evidence.md), [benchmarks](benchmark-results.md), and [size analysis](size-and-assembly.md) support its factual claims. This page tracks work and decisions; it is not part of the issue body.

## 1. Finish the repository as a public reference

- [x] Keep the shortest `float`/`double` producer and the local comparison adapter separately measurable.
- [x] Verify the compact cache against exact arithmetic and keep the full-table profile as a reconstruction diagnostic.
- [x] Record correctness sweeps, local timings, and JIT/data/image sizes with their limits.
- [x] License original repository work under MIT in Akito Inoue's name, while retaining [third-party notices](../THIRD-PARTY-NOTICES.txt) for reference and source material.
- [ ] Recover exact source revisions for any borrowed converter material, if possible. The pinned [upstream differential](../tools/verify_upstream.py) establishes behavior, not source provenance; [third-party notices](../THIRD-PARTY-NOTICES.txt) retain both reference licenses meanwhile.
- [ ] Repeat representative local benchmarks in multiple controlled sessions. Keep complete formatting, digit production, and unchanged integer controls separate; do not promote a near-parity ShortRun to a stable speed claim.
- [x] Make the repository public and add its immutable revision link to the [issue text](proposal.md).

The remaining provenance and repeated-measurement tasks do not require a `dotnet/runtime` fork.

## 2. Ask for scope before implementing a runtime patch

Post the [proposal](proposal.md) as a `dotnet/runtime` issue with [#131068](https://github.com/dotnet/runtime/pull/131068) linked. The decision sought is whether maintainers want to evaluate the narrower shortest-producer experiment and which baseline to use. If #131068 lands first, compare against it. Do not present the older local `UnroundedScaling.Comparison` snapshot as the current PR.

The initial experiment should change **only finite, unconstrained shortest `float`/`double` digit production**. Its output is digits and scale consumed by CoreLib's existing `NumberBuffer` and presentation path. Preserve existing handling for counted precision, other formats, cultures, non-finite values, and failed destination writes. The standalone `ZmijFormatter`, `RuntimeShim`, and comparison project are test infrastructure, not proposed runtime components.

## 3. If the issue supports an experiment, use matched runtime builds

This stage requires runtime source builds and is intentionally outside the current no-fork work.

1. Adapt `ZmijCore` and the generated power cache to current CoreLib conventions, with exact source provenance and license text. Keep the existing producer available during differential testing.
2. Build a matched baseline from current `main`, the relevant #131068 revision, and the candidate. Record source commits, SDK/runtime versions, architecture, configuration, and benchmark inputs for every result.
3. Classify output differences with an independent shortest-roundtrip oracle. Run exhaustive binary32 and large structured and random binary64 coverage, then verify `ToString`, UTF-16 and UTF-8 `TryFormat`, `Utf8Formatter`, cultures, special values, precision, custom formats, destination failures, and exceptions.
4. Measure digits-only, complete formatting, and the same `System.Text.Json` consumer on each build. Include common values, JSON-like magnitudes, uniform IEEE bits, long significands, powers of two and ten, exponent transitions, and subnormals. Use unchanged integer and JSON controls to detect run drift; report repeated paired runs rather than only best cases.
5. Measure x64 and ARM64 JIT, tiering/cold start, ReadyToRun, NativeAOT, relevant Mono/wasm behavior, allocations, and CoreLib image/table/native-code size. Compare compact and full cache profiles where the build supports both.

The local size harness deliberately uses a `Span<byte>` digit target for `double`; a second character writer would add presentation code to this producer measurement. If maintainers want `Half` or `BFloat16` in the candidate scope, add real 16-bit shortest producers and exhaust all 65,536 encodings of each type before reporting a four-type DLL or native-code comparison. Widening a 16-bit value to `float` would not establish its shortest 16-bit representation.

The follow-up PR is justified only if correctness and compatibility hold and the measured benefit is useful relative to code size and maintenance cost. If the producer does not meet that bar, stop without a runtime change. Evaluate direct destination emission as a separate experiment only if a CoreLib implementation improves complete formatting; the local direct UTF-8 attempt in [benchmark results](benchmark-results.md) did not.

## Decision record to keep updated

| Decision | Current position | Revisit when |
|---|---|---|
| Runtime scope | `float`/`double` unconstrained shortest producer | Maintainer response to the issue |
| Cache profile | Compact is the candidate; full table is a local reconstruction diagnostic | Matched CoreLib size/performance results exist |
| Counted precision | Delegate to the runtime in this repository | A separate fixed-width implementation is proposed and measured |
| Direct output | No retained local variant | Complete-path CoreLib evidence shows a gain |
| #131068 baseline | Current local port is an older snapshot | A current matched runtime comparison is available |
| `Half`/`BFloat16` | Outside the initial Zmij candidate; #131068 shares its cached-power table across all four types | Maintainers request a four-type candidate |

Update the [evidence record](evidence.md) only when a check has actually run; update this plan when the next action or decision changes.
