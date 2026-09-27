using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZmijSharp;

// Experimental port of xjb_comp_f32_to_dec from xjb714/xjb, Apache-2.0.
// This is a comparison producer, not a public formatting path. The source
// returns a possibly zero-terminated significand; normalize for our contract.
internal static class XjbFloatComparison
{
    private const ulong FractionMask = (1UL << 36) - 1;

    internal static ZmijDecimal ToDecimal(float value)
        => ToDecimal(value, false);

    internal static ZmijDecimal ToDecimalDirect(float value)
        => ToDecimal(value, true);

    private static ZmijDecimal ToDecimal(float value, bool directCache)
    {
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        uint fraction = bits & 0x7f_ffff;
        int exponent = (int)((bits >> 23) & 0xff);
        bool negative = (bits >> 31) != 0;
        if (exponent == 0xff)
            return new ZmijDecimal(fraction, int.MaxValue, negative);
        if (exponent == 0 && fraction == 0)
            return new ZmijDecimal(0, 0, negative);

        bool regular = fraction != 0;
        int binaryExponent = (exponent == 0 ? 1 : exponent) - 150;
        ulong significand = exponent == 0 ? fraction : fraction | (1U << 23);
        int decimalExponent = (binaryExponent * 315653 - (regular ? 0 : 131237)) >> 20;
        int powerExponent = -1 - decimalExponent;
        Debug.Assert(powerExponent is >= -32 and <= 44);

        int h = binaryExponent + ((powerExponent * 1701) >> 9);
        ulong power;
        if (directCache)
            power = XjbFloatDirectCache.Get(powerExponent);
        else
        {
            int block = (powerExponent + 32) >> 4;
            int anchorExponent = (block << 4) - 32;
            int residue = powerExponent - anchorExponent;
            int shift = ((powerExponent * 1701) >> 9) - residue - ((anchorExponent * 1701) >> 9);
            Debug.Assert(shift is >= 0 and < 64);
            ulong anchor = Unsafe.Add(ref MemoryMarshal.GetReference(Anchors), block);
            ulong minor = Minor(residue);
            ulong productHigh = Math.BigMul(anchor, minor, out ulong productLow);
            power = shift == 0 ? productLow : (productHigh << (64 - shift)) | (productLow >> shift);
        }

        ulong even = (significand + 1) & 1;
        ulong cb = significand << (h + 37);
        ulong scaled = Math.BigMul(cb, power, out _);
        ulong fraction36 = scaled & FractionMask;
        ulong halfUlp = power >> (28 - h);
        ulong offset = (1UL << 34) - 7 + (fraction36 >> 32);
        ulong lastDigit = (fraction36 * 5 + offset) >> 35;

        if (regular)
        {
            lastDigit = halfUlp + even > fraction36 ? 0 : lastDigit;
            lastDigit = halfUlp + even > FractionMask - fraction36 ? 10 : lastDigit;
        }
        else
        {
            lastDigit = (halfUlp >> 1) > fraction36 ? 0 : lastDigit;
            if (binaryExponent is -119 or 64 or 67)
                lastDigit++;
            lastDigit = halfUlp > FractionMask - fraction36 ? 10 : lastDigit;
        }

        ulong digits = (scaled >> 36) * 10 + lastDigit;
        return ZmijDecimal.CreateNormalized(digits, decimalExponent, negative);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Minor(int residue)
    {
        ref ulong packed = ref MemoryMarshal.GetReference(PackedMinor);
        ulong baseWord = Unsafe.Add(ref packed, residue >> 3);
        uint basePower = (uint)(baseWord >> ((residue & 4) * 8));
        ulong small = Unsafe.Add(ref packed, 2) >> ((residue & 3) * 8);
        return (ulong)basePower * (small & 0xff);
    }

    // Five anchors (40 bytes) and three packed minor words (24 bytes).
    private static ReadOnlySpan<ulong> Anchors =>
    [
        0xcfb11ead453994bbUL, 0xe69594bec44de15cUL,
        0x8000000000000000UL, 0x8e1bc9bf04000000UL,
        0x9dc5ada82b70b59eUL,
    ];

    private static ReadOnlySpan<ulong> PackedMinor =>
    [
        1UL | (625UL << 32),
        390625UL | (244140625UL << 32),
        1UL | (5UL << 8) | (25UL << 16) | (125UL << 24),
    ];
}
