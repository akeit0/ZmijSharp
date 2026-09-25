#!/usr/bin/env python3
"""Re-derive the compact Pow10 cache from integer interval constraints.

Reads src/ZmijSharp/CompactPow10Cache.cs,
recomputes every constant with exact integer arithmetic, and asserts
bit-identity. No dependencies beyond the standard library.

Checks:
  1. Exact cache entries P_q = floor(10^q * 2^(127 - g(q))) for q = -293..324.
  2. Normalized minor factors M_r for the configured stride.
  3. Anchor interval containment for every block.
  4. Reconstruction-minus-fixup equals P_q with no high-word borrow.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "src/ZmijSharp/CompactPow10Cache.cs"

MIN_Q, MAX_Q = -293, 324


def ulongs(text):
    return [int(m, 16) for m in re.findall(r"0x([0-9A-Fa-f]+)UL", text)]


def section(path, start_marker, names):
    text = path.read_text(encoding="utf-8-sig")
    start = text.index(start_marker)
    end = text.index("];", start)
    chunk = text[start:end]
    for name in names:
        assert name in chunk, name
    return chunk


def g(q):
    """floor(log2(10^q)) computed exactly."""
    if q >= 0:
        return pow(10, q).bit_length() - 1
    return -(pow(10, -q) - 1).bit_length()


def exact_entry(q):
    """P_q as a (high, low) pair of 64-bit words."""
    if q >= 0:
        shift = g(q) - 127
        # Power-of-two scaling: >> is exact when shift > 0.
        p = pow(10, q) >> shift if shift > 0 else pow(10, q) << -shift
    else:
        p = (1 << (127 - g(q))) // pow(10, -q)
    assert p.bit_length() <= 128
    return (p >> 64) & ((1 << 64) - 1), p & ((1 << 64) - 1)


def main():
    source = CACHE.read_text(encoding="utf-8-sig")
    stride = int(re.search(r"private const int Stride = (\d+);", source).group(1))
    anchor_start = int(re.search(r"private const int AnchorStart = (-?\d+);", source).group(1))
    assert anchor_start <= MIN_Q and (stride & (stride - 1)) == 0
    blocks = (MAX_Q - anchor_start) // stride + 1
    anchors = ulongs(section(CACHE, "Anchors =>", [f"q0 = {anchor_start}"]))
    assert len(anchors) == 2 * blocks, len(anchors)
    minors = ulongs(section(CACHE, "Minor =>", ["0x8000000000000000UL"]))
    assert len(minors) == stride, len(minors)
    fixups = [int(m, 16) for m in re.findall(r"0x([0-9A-Fa-f]{2})", section(CACHE, "Fixups =>", ["0x"]))]
    assert len(fixups) == 78, len(fixups)

    # 2. Normalized minors are exact: M_r is 5^r normalized into 64 bits.
    for r in range(stride):
        p5 = pow(5, r)
        assert minors[r] == (p5 << (63 - (p5.bit_length() - 1))), r

    # 1/3/4. Entries, anchor intervals, reconstruction.
    corrections = {0: 0, 1: 0}
    for qi in range(MIN_Q, MAX_Q + 1):
        i = qi - MIN_Q
        block, r = divmod(qi - anchor_start, stride)
        q0 = anchor_start + stride * block
        s = 63 + g(qi) - g(q0) - g(r)
        assert s in (63, 64), (qi, s)
        hi, lo = exact_entry(qi)
        p = (hi << 64) | lo
        m = minors[r]
        anchor = (anchors[2 * block] << 64) | anchors[2 * block + 1]
        # Interval the anchor must satisfy for this entry.
        assert ((p << s) + m - 1) // m <= anchor <= (((p + 2) << s) - 1) // m, qi
        # Reconstruction followed by the fixup bit.
        prod = anchor * m
        c2 = prod >> 128
        c1 = (prod >> 64) & ((1 << 64) - 1)
        c0 = prod & ((1 << 64) - 1)
        if c2 >> 63:
            rh, rl = c2 & ((1 << 64) - 1), c1
        else:
            rh = ((c2 << 1) | (c1 >> 63)) & ((1 << 64) - 1)
            rl = ((c1 << 1) | (c0 >> 63)) & ((1 << 64) - 1)
        fix = (fixups[i >> 3] >> (i & 7)) & 1
        corrections[fix] += 1
        assert rl >= fix, qi  # no high-word borrow, matching the generated C#
        assert rh == hi and rl - fix == lo, qi

    print(f"entries: 618 ok; corrections without fixup: {corrections[0]}, with fixup: {corrections[1]}")
    print("ALL CHECKS PASSED")


if __name__ == "__main__":
    sys.exit(main())
