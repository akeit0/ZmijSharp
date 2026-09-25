#!/usr/bin/env python3
"""Generate the stride-16 compact cache using exact integer intervals."""

import re
from pathlib import Path

from verify_compact_cache import exact_entry, g

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "src/ZmijSharp/CompactPow10Cache.cs"
MIN_Q, MAX_Q = -293, 324
ANCHOR_START, STRIDE = -298, 16
MASK = (1 << 64) - 1


def entry(q):
    hi, lo = exact_entry(q)
    return (hi << 64) | lo


def minor(r):
    p = 5**r
    return p << (63 - (p.bit_length() - 1))


minors = [minor(r) for r in range(STRIDE)]
anchors = []
fixups = bytearray((MAX_Q - MIN_Q + 8) // 8)
counts = [0, 0]

for block in range((MAX_Q - ANCHOR_START) // STRIDE + 1):
    q0 = ANCHOR_START + block * STRIDE
    first = max(MIN_Q, q0)
    last = min(MAX_Q, q0 + STRIDE - 1)
    lo, hi = 0, (1 << 128) - 1
    for q in range(first, last + 1):
        r = q - q0
        shift = 63 + g(q) - g(q0) - g(r)
        assert shift in (63, 64), (q, shift)
        p, m = entry(q), minors[r]
        lo = max(lo, ((p << shift) + m - 1) // m)
        hi = min(hi, (((p + 2) << shift) - 1) // m)
    assert lo <= hi, q0
    anchor = min(max(entry(q0), lo), hi)
    assert anchor.bit_length() == 128, q0
    anchors.append(anchor)

    for q in range(first, last + 1):
        r = q - q0
        shift = 63 + g(q) - g(q0) - g(r)
        produced = anchor * minors[r] >> shift
        correction = produced - entry(q)
        assert correction in (0, 1), (q, correction)
        assert (produced & MASK) >= correction, q
        counts[correction] += 1
        if correction:
            index = q - MIN_Q
            fixups[index >> 3] |= 1 << (index & 7)

assert sum(counts) == MAX_Q - MIN_Q + 1

anchor_lines = [
    f"        0x{a >> 64:016X}UL, 0x{a & MASK:016X}UL, // q0 = {ANCHOR_START + i * STRIDE}"
    for i, a in enumerate(anchors)
]
minor_lines = [
    "        " + " ".join(f"0x{m:016X}UL," for m in minors[i:i + 4])
    for i in range(0, len(minors), 4)
]
fixup_lines = [
    "        " + " ".join(f"0x{b:02X}," for b in fixups[i:i + 12])
    for i in range(0, len(fixups), 12)
]

source = CACHE.read_text(encoding="utf-8-sig")
for name, kind, lines in (
    ("Anchors", "ulong", anchor_lines),
    ("Minor", "ulong", minor_lines),
    ("Fixups", "byte", fixup_lines),
):
    pattern = rf"    // [^\n]*\n    private static ReadOnlySpan<{kind}> {name} =>\n    \[.*?\n    \];"
    size = len(anchors) * 16 if name == "Anchors" else len(minors) * 8 if name == "Minor" else len(fixups)
    replacement = f"    // {size} raw bytes.\n    private static ReadOnlySpan<{kind}> {name} =>\n    [\n" + "\n".join(lines) + "\n    ];"
    source, count = re.subn(pattern, lambda _: replacement, source, count=1, flags=re.S)
    assert count == 1, name

CACHE.write_text(source, encoding="utf-8")
print(f"anchors={len(anchors)}, minors={len(minors)}, fixups={len(fixups)}, corrections={counts}")
