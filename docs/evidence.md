# Completed local evidence

This page records checks that have actually run in this repository. The [proposal](proposal.md) selects the claims to present publicly; the [work plan](plan.md) tracks next steps and runtime-only gates. The [component benchmarks](component-benchmarks.md) and [size analysis](size-and-assembly.md) contain measurement details. Unless noted, local runs used the SDK in [`global.json`](../global.json) on Windows x64 and compared with the installed .NET 11 RC runtime.

## Correctness completed locally

| Check | Completed run | What it establishes |
|---|---|---|
| Default verifier | 100,000 deterministic random patterns per type; 2,000 independent `BigInteger` shortest-decimal oracle cases per type; all binary-exponent boundary oracle cases | Exact output on the sampled cases, plus independent shortestness/canonical checks on a smaller corpus |
| Exhaustive binary32 output | Four shards, covering all 2³² encodings; default invariant `char` `TryFormat` compared byte-for-byte with the installed runtime | Complete default-output equivalence for `float` on this runtime, including zero and non-finite encodings |
| Binary64 output | 100,000,000 deterministic raw patterns; default invariant `char` and UTF-8 output compared with the installed runtime | Broad sampled output equivalence for `double`, not exhaustive binary64 proof |
| Upstream Żmij differential | 1,000,000 random patterns per type plus all binary-exponent boundary patterns against pinned `vitaut/zmij` `d1682cb` | 1,015,896 finite `double` and 998,157 finite `float` decompositions agree after removing trailing zeros from significands; 785,022 raw tuple differences were trailing-zero representation only |
| Format and destination matrix | Invariant, `fr-FR`, `sv-SE`, and a custom provider with multicharacter/supplementary-plane symbols; 2,048 random values per type/provider, 14 formats, and every failed/exact capacity for edge values | Local formatter output matches the installed runtime on this matrix and leaves failed `char` destinations untouched |
| Power cache | All 618 entries checked against exact integer arithmetic in the verifier; `tools/verify_compact_cache.py` independently re-derives compact reconstruction | Compiled compact table identity; the same verifier can be run with `ZmijCache=Full` |
| Unit tests | 11 TUnit tests | Targeted formatting, fallback, shim, precision, and destination behavior |

The binary32 sweep is an output comparison against one runtime, not an independent shortestness proof for all inputs. The `BigInteger` oracle and upstream differential provide independent checks on sampled and structured inputs. A runtime branch must classify any output differences from newer `main`; baseline output should not be assumed infallible.

## Local reproduction

Run from the repository root:

```bash
dotnet test -c Release --project tests/ZmijSharp.Tests
dotnet run -c Release --project tests/ZmijSharp.Verify
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --format-matrix
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --double-output 100000000
python tools/verify_compact_cache.py
python tools/verify_upstream.py --count 1000000
```

For the exhaustive binary32 output check, run each shard 0 through 3 separately (replace the first number):

```bash
dotnet run -c Release --project tests/ZmijSharp.Verify -- --producer-only --float-output-exhaustive 0 4
```

The older `--float-exhaustive` mode checks only producer shape; it remains useful as a cheaper diagnostic. The upstream script downloads immutable source files at the pinned commit and requires a C++20 compiler (`g++` by default).

## Performance and optimization decisions

- Two later [component sessions](component-benchmarks.md) compared canonical tuple production, pointer versus span digit buffers, and the comparison wrapper's byte-to-`char` staging. On corrected varied long significands and raw-bit inputs, the local #131068 specialization produced canonical tuples faster than Zmij. Buffer shape was near parity; staging bytes into `char` was costlier in the local adapter. These probes cannot be summed into the complete-path measurements.
- A direct UTF-8 emitter was implemented and measured against the existing buffered emitter on the same raw-bit corpus. DefaultJob means were 40.91 and 40.85 ns respectively; the variant was removed because it showed no benefit.
- The temporary `BigInteger` `G5` implementation measured 1,904 ns and 1,125 B per call versus 58 ns and no allocation for runtime `G5`. Explicit `G` precision now delegates to the runtime. A follow-up ShortRun measured 57.53 ns for that fallback versus 57.40 ns for direct runtime `G5`, both allocation-free.
- At the issue draft's measured revision, a source-linked minimal project reported 11,776 B for compact Zmij and 19,968 B for the pinned #131068 local port. Both builds round-tripped and produced identical digits and scales for 249,884 deterministic finite nonzero values; a separate 2,000,000-pattern and exponent-boundary agreement audit passed. The listed x64 JIT call trees were 2,087 and 1,391 B. See the [size and assembly record](size-and-assembly.md) for the current source selection and limitations.
- A later [digit-stage and x64 JIT experiment](optimization-notes.md) consolidated Zmij's finite-result normalization, reducing its listed x64 JIT call tree to 1,868 B while leaving its minimal DLL at 11,776 B. The same-input digit-helper microbenchmarks and component sessions have different scopes.
- A further private-result simplification reduced that listed minimal-profile x64 JIT call tree to 1,833 B; the minimal DLL remains 11,776 B. The 2,000,000-pattern plus exponent-boundary check, 11 unit tests, and minimal-profile digest passed. The decomposition ShortRun remained within earlier ranges, so no throughput gain is claimed. The [optimization notes](optimization-notes.md) also record the removed scaling/cache variants and isolated SSE2 digit-writing probe.
- An [alternating full/compact/full cache comparison](benchmark-sessions/cache-profiles-decomposition.md) showed that the full table improves local canonical decomposition while increasing the minimal DLL from 11,776 to 20,480 B. Both cache profiles produced the same 249,884-input digits-and-scale digest. The [size analysis](size-and-assembly.md#full-cache-diagnostic) gives source-data and JIT counts; these are not CoreLib measurements.
- A subsequent stride-16 compact cache and branchless normalization trial is retained with unchecked table reads on both cache profiles. At that revision, the minimal DLLs were 12,288 B compact and 20,480 B full; the x64 private conversion methods were 1,005 and 631 native bytes. Exact reconstruction of 618 powers, 11 unit tests in each profile, two-million-pattern plus exponent-boundary checks, and the 249,884-input digest passed. The earlier issue measurements remain pinned to their original revision; see [optimization notes](optimization-notes.md) for the separate timing runs.
- A later [normal-power-of-two specialization](optimization-notes.md#exact-powers-of-two) replaced two scaling products with shifts only in the irregular branch. At that revision, the compact and full private `double` methods measured 974 and 610 x64 native bytes. Focused [ShortRuns](benchmark-sessions/power-of-two-short.md) improved on both profiles; the minimal DLL lengths remained 12,288 and 20,480 B.
- Moving the decimal exponent, shift, and cached-power lookup into the `double` and `float` callers reduced the listed compact `double` JIT call tree from 1,742 to 1,532 bytes, with the minimal DLLs unchanged. A fresh [full-cache A/B](benchmark-sessions/shared-caller-short.md) measured random canonical `double` decomposition at 8.563 to 6.747 ns/value. The `float` results varied by workload and cache profile. Both profiles passed unit and oracle verification; the compact minimal project retained the same 249,884-input digest as the pinned #131068 local port.
- The [current `double` last-digit fast path](benchmark-sessions/nonzero-last-digit-short.md) skips normalization when the extra digit is nonzero. Separate ShortRun A/B launches improved long-significand and random canonical decomposition in both cache profiles, while simple inputs changed little. The minimal DLL lengths remain 12,288 B compact and 20,480 B full; the compact x64 listed call tree is 1,562 B. Both profiles passed unit tests, and the compact profile passed the two-million-pattern and exponent-boundary audit and the same digest as the pinned #131068 port.
- The [three-producer benchmark](benchmark-sessions/same-run-cache-profiles-short.md) now builds compact, full, and the pinned #131068 local port together. Its setup checked equal canonical tuples on each corpus. In one BenchmarkDotNet invocation, compact/full/#131068 measured **8.108 / 5.783 / 6.763 ns** on varied long significands and **10.175 / 6.058 / 8.880 ns** on random raw bits. BenchmarkDotNet still launches each case in its own process; the experiment remains a standalone `double` decomposition comparison.
