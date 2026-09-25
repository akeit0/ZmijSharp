# Completed local evidence

This page records checks that have actually run in this repository. The [proposal](proposal.md) selects the claims to present publicly; the [work plan](plan.md) tracks next steps and runtime-only gates. The [benchmark results](benchmark-results.md) and [size analysis](size-and-assembly.md) contain measurement details. Unless noted, local runs used the SDK in [`global.json`](../global.json) on Windows x64 and compared with the installed .NET 11 RC runtime.

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

- Two standalone `double` `TryFormat` ShortRun sessions compared the pinned #131068 local `double` port with Zmij and the installed runtime. The port led on JSON-like values (28.08–28.68 vs. 29.93–30.10 ns/value); Zmij led on long-significand values (24.40–24.68 vs. 33.57–39.79 ns/value) and uniform raw-bit values (44.72–45.48 vs. 49.82–51.38 ns/value). The simple-value order changed between sessions. These ranges are session means, and the local port does not measure the PR's CoreLib integration. See [methods, full tables, and raw sessions](benchmark-results.md).
- A direct UTF-8 emitter was implemented and measured against the existing buffered emitter on the same raw-bit corpus. DefaultJob means were 40.91 and 40.85 ns respectively; the variant was removed because it showed no benefit.
- The temporary `BigInteger` `G5` implementation measured 1,904 ns and 1,125 B per call versus 58 ns and no allocation for runtime `G5`. Explicit `G` precision now delegates to the runtime. A follow-up ShortRun measured 57.53 ns for that fallback versus 57.40 ns for direct runtime `G5`, both allocation-free.
- At the issue draft's measured revision, a source-linked minimal project reported 11,776 B for compact Zmij and 19,968 B for the pinned #131068 local port. Both builds round-tripped and produced identical digits and scales for 249,884 deterministic finite nonzero values; a separate 2,000,000-pattern and exponent-boundary agreement audit passed. The listed x64 JIT call trees were 2,087 and 1,391 B. See the [size and assembly record](size-and-assembly.md) for the current source selection and limitations.
- A later [digit-stage and x64 JIT experiment](optimization-notes.md) consolidated Zmij's finite-result normalization, reducing its listed x64 JIT call tree to 1,868 B while leaving its minimal DLL at 11,776 B. The same-input digit-helper microbenchmarks and complete-path benchmark sessions have different scopes; the issue draft retains links to its immutable measured revision.
