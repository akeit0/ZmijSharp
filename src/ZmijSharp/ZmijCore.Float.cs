using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZmijSharp;

internal static partial class ZmijCore
{
    private const int FloatSignificandBits = 23;
    private const int FloatExponentBits = 8;
    private const int FloatExponentMask = (1 << FloatExponentBits) - 1;
    private const int FloatExponentBias = (1 << (FloatExponentBits - 1)) - 1;
    private const int FloatExponentOffset = FloatExponentBias + FloatSignificandBits;
    private const uint FloatImplicitBit = 1U << FloatSignificandBits;

    internal static ZmijDecimal ToDecimal(float value)
    {
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        int binExp = GetFloatExponent(bits);
        uint binSig = GetFloatSignificand(bits);
        bool negative = IsNegative(bits);

        if (binExp == 0 || binExp == FloatExponentMask)
        {
            if (binExp != 0)
                return new ZmijDecimal(binSig, NonFiniteExponent, negative);

            if (binSig == 0)
                return new ZmijDecimal(0, 0, negative);

            binExp = 1;
            binSig |= FloatImplicitBit;
        }

        int adjustedBinExp = binExp - FloatExponentOffset;
        int decimalExp = ComputeDecimalExponent(adjustedBinExp, binSig != 0);
        int shift = ComputeExponentShift(adjustedBinExp, decimalExp + 1) + ExtraShift;
        GetPowerOf10(-decimalExp - 1, out ulong powHigh, out ulong powLow);
        DecimalResult dec = ToDecimalFloat(binSig ^ FloatImplicitBit, decimalExp, shift, powHigh, powLow);
        ulong significand;
        int resultExponent;
        if (dec.HasLastDigit)
        {
            significand = dec.Significand * 10 + (uint)dec.LastDigit;
            resultExponent = dec.Exponent;
        }
        else
        {
            significand = dec.Significand;
            resultExponent = dec.Exponent + 1;
        }
        return ZmijDecimal.CreateNormalized(significand, resultExponent, negative);
    }

    private static DecimalResult ToDecimalFloat(uint binSig, int decimalExp, int shift, ulong powHigh, ulong powLow)
    {
        if (binSig == FloatImplicitBit)
        {
            int powerShift = FloatSignificandBits + shift;
            Debug.Assert(powerShift is >= 27 and <= 30);
            ulong irregularProductHigh = powHigh >> (64 - powerShift);
            ulong irregularProductLow = (powHigh << powerShift)
                | (powLow >> (64 - powerShift));

            ulong integral = irregularProductHigh >> ExtraShift;
            ulong fractional = (irregularProductHigh << (64 - ExtraShift)) | (irregularProductLow >> ExtraShift);
            ulong halfUlp = powHigh >> (ExtraShift + 1 - shift);
            bool roundUp = halfUlp > ulong.MaxValue - fractional;
            bool roundDown = (halfUlp >> 1) > fractional;
            integral += roundUp ? 1UL : 0UL;

            int digit = (int)Multiply128AddHigh64(fractional, 10, 0x7fff_ffff_ffff_ffffUL);
            int lo = (int)Multiply128AddHigh64(fractional - (halfUlp >> 1), 10, ulong.MaxValue);
            if (digit < lo)
                digit = lo;

            return new DecimalResult(integral, decimalExp, digit, !roundUp && !roundDown);
        }

        ulong even = 1UL - (binSig & 1U);

        const int floatExtraShift = 34;
        shift += floatExtraShift - ExtraShift;
        ulong product = Math.BigMul(powHigh + 1, (ulong)binSig << shift, out _);

        ulong integralPart = product >> floatExtraShift;
        ulong fractionalPart = product & ((1UL << floatExtraShift) - 1);
        ulong halfUlpRegular = (powHigh >> (65 - shift)) + even;
        bool roundUpRegular = ((fractionalPart + halfUlpRegular) >> floatExtraShift) != 0;
        bool roundDownRegular = halfUlpRegular > fractionalPart;
        integralPart += roundUpRegular ? 1UL : 0UL;

        int extraDigit = (int)((fractionalPart * 10 + (1UL << (floatExtraShift - 1))) >> floatExtraShift);
        if (fractionalPart == (1UL << (floatExtraShift - 2)))
            extraDigit = 2;

        return new DecimalResult(integralPart, decimalExp, extraDigit, !roundUpRegular && !roundDownRegular);
    }

    internal static bool TryGetSignificantDigits(float value, Span<byte> destination, out int digitCount, out int scale, out bool negative)
    {
        if (!float.IsFinite(value))
        {
            digitCount = 0;
            scale = 0;
            negative = false;
            return false;
        }

        return TryGetSignificantDigits(ToDecimal(value), destination, out digitCount, out scale, out negative);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNegative(uint bits) => (bits >> 31) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetFloatSignificand(uint bits) => bits & (FloatImplicitBit - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFloatExponent(uint bits) => (int)((bits << 1) >> (FloatSignificandBits + 1));
}
