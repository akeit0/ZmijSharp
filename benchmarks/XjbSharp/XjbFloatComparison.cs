namespace ZmijSharp;

// Benchmark-only adaptation of xjb_v2_f32_to_dec at xjb714/xjb 80cc895.
// See XJB-LICENSE.txt and THIRD-PARTY-NOTICES.txt.
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

        // Keep the same exact-integer shortcut as the Żmij benchmark path.
        if (exponent != 0)
        {
            int integerShift = 150 - exponent;
            if ((uint)integerShift <= 23U
                && (fraction & ((1U << integerShift) - 1)) == 0)
                return ZmijDecimal.CreateNormalized((fraction | (1U << 23)) >> integerShift, 0, negative);
        }

        int q = (exponent == 0 ? 1 : exponent) - 150;
        if (fraction == 0)
        {
            int k = (q * 1233 - 512) >> 12;
            int h = q + ((-k * 1701 - 1701) >> 9);
            ulong power = XjbFloatDirectCache.Get(-1 - k);
            ulong hi = power >> (4 - h);
            ulong frac = hi & FractionMask;
            ulong halfUlp = power >> (28 - h);
            ulong one = (frac * 5 + ((1UL << 34) - 7) + (frac >> 32)) >> 35;
            if ((halfUlp >> 1) > frac)
                one = 0;
            if (q is -119 or 64 or 67)
                one++;
            if (halfUlp > FractionMask - frac)
                one = 10;
            ulong digits = (hi >> 36) * 10 + one;
            return ZmijDecimal.CreateNormalized(digits, k, negative);
        }

        int decimalExponent = (q * 1233) >> 12;
        int h37 = XjbFloatH37.Get(exponent);
        ulong powerHigh = XjbFloatDirectCache.Get(-1 - decimalExponent);
        ulong significand = exponent == 0 ? fraction : fraction | (1U << 23);
        ulong cb = significand << h37;
        ulong hi64 = Math.BigMul(cb, powerHigh, out _);
        ulong half = (powerHigh >> (65 - h37)) + ((significand + 1) & 1);
        ulong shorter = ((hi64 + half) >> 36) * 10;
        ulong longer = (hi64 * 5 + ((1UL << 34) - 7) + ((hi64 >> 32) & 15)) >> 35;
        ulong decimalSignificand = ((hi64 - half) >> 36) < ((hi64 + half) >> 36) ? shorter : longer;
        return ZmijDecimal.CreateNormalized(decimalSignificand, decimalExponent, negative);
    }
}
