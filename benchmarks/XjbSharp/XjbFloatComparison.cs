using System.Diagnostics;

namespace ZmijSharp;

// Experimental port of xjb_comp_f32_to_dec from xjb714/xjb, Apache-2.0.
// This is a comparison producer, not a public formatting path. The source
// returns a possibly zero-terminated significand; normalize for our contract.
internal static class XjbFloatComparison
{
    private const ulong FractionMask = (1UL << 36) - 1;

    internal static ZmijDecimal ToDecimal(float value)
    {
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        uint fraction = bits & 0x7f_ffff;
        int exponent = (int)((bits >> 23) & 0xff);
        bool negative = (bits >> 31) != 0;
        if (exponent == 0xff)
            return new ZmijDecimal(fraction, int.MaxValue, negative);
        if (exponent == 0 && fraction == 0)
            return new ZmijDecimal(0, 0, negative);

        // Match the small-integer shortcut used by the Żmij producer.
        if (exponent != 0)
        {
            int integerShift = 150 - exponent;
            if ((uint)integerShift <= 23U
                && (fraction & ((1U << integerShift) - 1)) == 0)
                return ZmijDecimal.CreateNormalized((fraction | (1U << 23)) >> integerShift, 0, negative);
        }

        bool regular = fraction != 0;
        int binaryExponent = (exponent == 0 ? 1 : exponent) - 150;
        ulong significand = exponent == 0 ? fraction : fraction | (1U << 23);
        int decimalExponent = (binaryExponent * 315653 - (regular ? 0 : 131237)) >> 20;
        int powerExponent = -1 - decimalExponent;
        Debug.Assert(powerExponent is >= -32 and <= 44);

        int h = binaryExponent + ((powerExponent * 1701) >> 9);
        ulong power = XjbFloatDirectCache.Get(powerExponent);

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

}
