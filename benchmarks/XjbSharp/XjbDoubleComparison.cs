using System.Diagnostics;
using ZmijSharp;

namespace ZmijSharp;

// Benchmark-only port of xjb64_v2_f64_to_dec from xjb714/xjb at 80cc895.
// The xjb direct cache is generated from the same mathematical power set as
// Zmij's full cache, with xjb's own rounding for non-exact entries.
// See XJB-LICENSE.txt and THIRD-PARTY-NOTICES.txt.
internal static class XjbDoubleComparison
{
    internal static ZmijDecimal ToDecimal(double value)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        ulong fraction = bits & ((1UL << 52) - 1);
        int exponent = (int)((bits >> 52) & 0x7ff);
        bool negative = (bits >> 63) != 0;
        if (exponent == 0x7ff)
            return new ZmijDecimal(fraction, int.MaxValue, negative);
        if (exponent == 0 && fraction == 0)
            return new ZmijDecimal(0, 0, negative);

        // Apply the same algorithm-independent small-integer shortcut as the
        // current Zmij path so producer timings have the same input contract.
        if (exponent != 0)
        {
            int integerShift = 1075 - exponent;
            if ((uint)integerShift <= 52U
                && (fraction & ((1UL << integerShift) - 1)) == 0)
                return ZmijDecimal.CreateNormalized((fraction | (1UL << 52)) >> integerShift, 0, negative);
        }

        bool irregular = exponent != 0 && fraction == 0;
        ulong significand = exponent == 0 ? fraction : fraction | (1UL << 52);
        int binaryExponent = (exponent == 0 ? 1 : exponent) - 1075;
        int decimalExponent = irregular
            ? (binaryExponent * 315653 - 131072) >> 20
            : (binaryExponent * 78913) >> 18;
        int powerExponent = -decimalExponent - 1;
        Debug.Assert(powerExponent is >= -293 and <= 323);
        int h = binaryExponent + ((powerExponent * 217707) >> 16);
#if XJB_DOUBLE_COMPACT
        CompactPow10Cache.Get(powerExponent, out ulong powerHigh, out ulong powerLow);
        if (powerExponent < 0 || powerExponent > 55)
        {
            Debug.Assert(powerLow != ulong.MaxValue);
            powerLow++;
        }
#else
        XjbDoubleCache.Get(powerExponent, out ulong powerHigh, out ulong powerLow);
#endif

        ulong digits;
        if (irregular)
        {
            ulong halfUlp = powerHigh >> -h;
            ulong dotOne = powerHigh << (53 + h);
            ulong ten = (powerHigh >> (11 - h)) * 10;
            ulong one = ((dotOne >> (53 + h)) * 5 + (1UL << (9 - h))) >> (10 - h);
            if ((((dotOne >> 54) * 5) & 511) > ((halfUlp >> 55) * 5))
                one = (((dotOne >> 54) * 5) >> 9) + 1;
            if (dotOne == (1UL << 62))
                one = 2;
            one = (halfUlp >> 1) > dotOne ? 0 : one;
            one = halfUlp > ulong.MaxValue - dotOne ? 10 : one;
            digits = ten + one;
        }
        else
        {
            ulong cb = significand << (h + 7);
            ulong high = Math.BigMul(cb, powerHigh, out ulong low);
            ulong lowHigh = Math.BigMul(cb, powerLow, out _);
            low += lowHigh;
            high += low < lowHigh ? 1UL : 0UL;
            ulong dotOne = (high << 58) | (low >> 6);
            ulong halfUlp = (powerHigh >> -h) + ((bits + 1) & 1);
            ulong ten = (high >> 6) * 10;
            ulong bias = dotOne == (1UL << 62) ? 0 : (1UL << 63) + 6;
            ulong one = Math.BigMul(dotOne, 10, out ulong digitLow);
            digitLow += bias;
            one += digitLow < bias ? 1UL : 0UL;
            one = dotOne < halfUlp ? 0 : one;
            one = ulong.MaxValue - dotOne < halfUlp ? 10 : one;
            digits = ten + one;
        }

        return ZmijDecimal.CreateNormalized(digits, decimalExponent, negative);
    }
}
