using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZmijSharp;

// Żmij-style conversion core; Zmij.NET was a C# reference. See third-party notices.
// This file intentionally stops at binary -> shortest decimal decomposition.
// Presentation policy is implemented separately in ZmijFormatter.cs.
internal static partial class ZmijCore
{
    private const int NonFiniteExponent = int.MaxValue;
    private const int DoubleSignificandBits = 52;
    private const int DoubleExponentBits = 11;
    private const int DoubleExponentMask = (1 << DoubleExponentBits) - 1;
    private const int DoubleExponentBias = (1 << (DoubleExponentBits - 1)) - 1;
    private const int DoubleExponentOffset = DoubleExponentBias + DoubleSignificandBits;
    private const ulong DoubleImplicitBit = 1UL << DoubleSignificandBits;

    private const int ExtraShift = 6;
    private const ulong Threshold = 1_000_000_000_000_000UL;
    private const ulong BiasedHalf = 0x8000_0000_0000_0006UL;

    internal static ZmijDecimal ToDecimal(double value)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        int binExp = GetExponent(bits);
        ulong binSig = GetSignificand(bits);
        bool negative = IsNegative(bits);

        if (binExp == 0 || binExp == DoubleExponentMask)
        {
            if (binExp != 0)
                return new ZmijDecimal(binSig, NonFiniteExponent, negative, normalize: false);

            if (binSig == 0)
                return new ZmijDecimal(0, 0, negative);

            binExp = 1;
            binSig |= DoubleImplicitBit;
        }

        DecimalResult dec = ToDecimal(binSig ^ DoubleImplicitBit, binExp, binSig != 0);
        ulong significand;
        int decimalExponent;
        if (dec.HasLastDigit)
        {
            significand = dec.Significand * 10 + (uint)dec.LastDigit;
            decimalExponent = dec.Exponent;
        }
        else
        {
            significand = dec.Significand;
            decimalExponent = dec.Exponent + 1;
        }
        return new ZmijDecimal(significand, decimalExponent, negative);
    }

    private static DecimalResult ToDecimal(ulong binSig, int rawExp, bool regular)
    {
        int binExp = rawExp - DoubleExponentOffset;
        if (!regular)
        {
            int decExp = ComputeDecimalExponent(binExp, regular: false);
            int irregularShift = ComputeExponentShift(binExp, decExp + 1) + ExtraShift;
            GetPowerOf10(-decExp - 1, out ulong irregularPow10High, out ulong irregularPow10Low);
            ulong irregularY = binSig << irregularShift;
            ulong irregularPHigh = Math.BigMul(irregularPow10High, irregularY, out ulong irregularPLow);
            ulong irregularLowProductHigh = Math.BigMul(irregularPow10Low, irregularY, out _);
            ulong irregularProductLow = irregularPLow + irregularLowProductHigh;
            ulong irregularProductHigh = irregularPHigh + (irregularProductLow < irregularPLow ? 1UL : 0UL);

            ulong integral = irregularProductHigh >> ExtraShift;
            ulong fractional = (irregularProductHigh << (64 - ExtraShift)) | (irregularProductLow >> ExtraShift);
            ulong halfUlp = irregularPow10High >> (ExtraShift + 1 - irregularShift);
            bool roundUp = halfUlp > ulong.MaxValue - fractional;
            bool roundDown = (halfUlp >> 1) > fractional;
            integral += roundUp ? 1UL : 0UL;

            int digit = (int)Multiply128AddHigh64(fractional, 10, 0x7fff_ffff_ffff_ffffUL);
            int lo = (int)Multiply128AddHigh64(fractional - (halfUlp >> 1), 10, ulong.MaxValue);
            if (digit < lo)
                digit = lo;

            return new DecimalResult(integral, decExp, digit, !roundUp && !roundDown);
        }

        int decimalExp = ComputeDecimalExponent(binExp);
        int shift = ComputeExponentShift(binExp, decimalExp + 1) + ExtraShift;
        ulong even = 1UL - (binSig & 1UL);
        GetPowerOf10(-decimalExp - 1, out ulong powHigh, out ulong powLow);
        ulong y = binSig << shift;
        ulong productHigh = Math.BigMul(powHigh, y, out ulong productLow);
        ulong lowProductHigh = Math.BigMul(powLow, y, out _);
        productLow += lowProductHigh;
        productHigh += productLow < lowProductHigh ? 1UL : 0UL;

        ulong integralPart = productHigh >> ExtraShift;
        ulong fractionalPart = (productHigh << (64 - ExtraShift)) | (productLow >> ExtraShift);
        ulong halfUlpRegular = (powHigh >> (ExtraShift + 1 - shift)) + even;
        bool roundUpRegular = fractionalPart + halfUlpRegular < fractionalPart;
        bool roundDownRegular = halfUlpRegular > fractionalPart;
        integralPart += roundUpRegular ? 1UL : 0UL;

        int extraDigit = (int)Multiply128AddHigh64(fractionalPart, 10, BiasedHalf);
        if (fractionalPart == (1UL << 62))
            extraDigit = 2;

        return new DecimalResult(integralPart, decimalExp, extraDigit, !roundUpRegular && !roundDownRegular);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeDecimalExponent(int binExp, bool regular = true)
    {
        const int log10ThreeOverFourSig = 131_072;
        const int log10TwoSig = 315_653;
        const int log10TwoExp = 20;
        return (binExp * log10TwoSig - (regular ? 0 : log10ThreeOverFourSig)) >> log10TwoExp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeExponentShift(int binExp, int decimalExp)
    {
        const int log2Pow10Sig = 217_707;
        const int log2Pow10Exp = 16;
        int pow10BinExp = -decimalExp * log2Pow10Sig >> log2Pow10Exp;
        return binExp + pow10BinExp + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetPowerOf10(int decimalExp, out ulong high, out ulong low)
    {
#if ZMIJ_FULL_TABLE
        int index = decimalExp + 293;
        int offset = index << 1;
        high = Pow10Tables.Significands[offset];
        low = Pow10Tables.Significands[offset + 1];
#else
        CompactPow10Cache.Get(decimalExp, out high, out low);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Multiply128AddHigh64(ulong x, ulong y, ulong c)
    {
        ulong high = Math.BigMul(x, y, out ulong low);
        low += c;
        return high + (low < c ? 1UL : 0UL);
    }

    internal static bool TryGetSignificantDigits(double value, Span<byte> destination, out int digitCount, out int scale, out bool negative)
    {
        if (!double.IsFinite(value))
        {
            digitCount = 0;
            scale = 0;
            negative = false;
            return false;
        }

        return TryGetSignificantDigits(ToDecimal(value), destination, out digitCount, out scale, out negative);
    }

    private static unsafe bool TryGetSignificantDigits(ZmijDecimal value, Span<byte> destination, out int digitCount, out int scale, out bool negative)
    {
        digitCount = value.Significand == 0 ? 0 : CountDigits(value.Significand);
        scale = value.Significand == 0 ? 0 : digitCount + value.Exponent;
        negative = value.IsNegative;
        if (digitCount > destination.Length)
        {
            digitCount = 0;
            scale = 0;
            negative = false;
            return false;
        }

        if (value.Significand == 0)
            return true;

        fixed (byte* output = destination)
        {
            byte* writer = output + digitCount;
            ulong significand = value.Significand;
            ref byte pairBase = ref MemoryMarshal.GetReference(DigitPairs);
            while (significand >= 100)
            {
                ulong quotient = significand / 100;
                int pair = (int)(significand - quotient * 100) * 2;
                significand = quotient;
                Debug.Assert(pair + 1 < DigitPairs.Length);
                writer -= 2;
                Unsafe.WriteUnaligned(writer, Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pairBase, pair)));
            }

            if (significand >= 10)
            {
                int pair = (int)significand * 2;
                Debug.Assert(pair + 1 < DigitPairs.Length);
                writer -= 2;
                Unsafe.WriteUnaligned(writer, Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pairBase, pair)));
            }
            else
            {
                *--writer = (byte)('0' + significand);
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CountDigits(ulong value)
    {
        // Callers handle zero separately; Log2 requires nonzero input.
        Debug.Assert(value != 0);
        int digits = ((BitOperations.Log2(value) * 1233) >> 12) + 1;
        if (value >= Pow10[digits])
            return digits + 1;
        return digits;
    }

    private static ReadOnlySpan<ulong> Pow10 =>
    [
        1UL, 10UL, 100UL, 1_000UL, 10_000UL, 100_000UL, 1_000_000UL, 10_000_000UL,
        100_000_000UL, 1_000_000_000UL, 10_000_000_000UL, 100_000_000_000UL,
        1_000_000_000_000UL, 10_000_000_000_000UL, 100_000_000_000_000UL,
        1_000_000_000_000_000UL, 10_000_000_000_000_000UL, 100_000_000_000_000_000UL,
        1_000_000_000_000_000_000UL, 10_000_000_000_000_000_000UL,
    ];

    // Shared ASCII rendering of decimal pairs 00..99 (200 bytes). Pair i starts at
    // offset 2 * i. Used here and by the presentation layer; the UTF-16 path widens
    // these bytes instead of carrying a second table.
    internal static ReadOnlySpan<byte> DigitPairs =>
    [
        (byte)'0', (byte)'0', (byte)'0', (byte)'1', (byte)'0', (byte)'2', (byte)'0', (byte)'3', (byte)'0', (byte)'4', (byte)'0', (byte)'5', (byte)'0', (byte)'6', (byte)'0', (byte)'7', (byte)'0', (byte)'8', (byte)'0', (byte)'9',
        (byte)'1', (byte)'0', (byte)'1', (byte)'1', (byte)'1', (byte)'2', (byte)'1', (byte)'3', (byte)'1', (byte)'4', (byte)'1', (byte)'5', (byte)'1', (byte)'6', (byte)'1', (byte)'7', (byte)'1', (byte)'8', (byte)'1', (byte)'9',
        (byte)'2', (byte)'0', (byte)'2', (byte)'1', (byte)'2', (byte)'2', (byte)'2', (byte)'3', (byte)'2', (byte)'4', (byte)'2', (byte)'5', (byte)'2', (byte)'6', (byte)'2', (byte)'7', (byte)'2', (byte)'8', (byte)'2', (byte)'9',
        (byte)'3', (byte)'0', (byte)'3', (byte)'1', (byte)'3', (byte)'2', (byte)'3', (byte)'3', (byte)'3', (byte)'4', (byte)'3', (byte)'5', (byte)'3', (byte)'6', (byte)'3', (byte)'7', (byte)'3', (byte)'8', (byte)'3', (byte)'9',
        (byte)'4', (byte)'0', (byte)'4', (byte)'1', (byte)'4', (byte)'2', (byte)'4', (byte)'3', (byte)'4', (byte)'4', (byte)'4', (byte)'5', (byte)'4', (byte)'6', (byte)'4', (byte)'7', (byte)'4', (byte)'8', (byte)'4', (byte)'9',
        (byte)'5', (byte)'0', (byte)'5', (byte)'1', (byte)'5', (byte)'2', (byte)'5', (byte)'3', (byte)'5', (byte)'4', (byte)'5', (byte)'5', (byte)'5', (byte)'6', (byte)'5', (byte)'7', (byte)'5', (byte)'8', (byte)'5', (byte)'9',
        (byte)'6', (byte)'0', (byte)'6', (byte)'1', (byte)'6', (byte)'2', (byte)'6', (byte)'3', (byte)'6', (byte)'4', (byte)'6', (byte)'5', (byte)'6', (byte)'6', (byte)'6', (byte)'7', (byte)'6', (byte)'8', (byte)'6', (byte)'9',
        (byte)'7', (byte)'0', (byte)'7', (byte)'1', (byte)'7', (byte)'2', (byte)'7', (byte)'3', (byte)'7', (byte)'4', (byte)'7', (byte)'5', (byte)'7', (byte)'6', (byte)'7', (byte)'7', (byte)'7', (byte)'8', (byte)'7', (byte)'9',
        (byte)'8', (byte)'0', (byte)'8', (byte)'1', (byte)'8', (byte)'2', (byte)'8', (byte)'3', (byte)'8', (byte)'4', (byte)'8', (byte)'5', (byte)'8', (byte)'6', (byte)'8', (byte)'7', (byte)'8', (byte)'8', (byte)'8', (byte)'9',
        (byte)'9', (byte)'0', (byte)'9', (byte)'1', (byte)'9', (byte)'2', (byte)'9', (byte)'3', (byte)'9', (byte)'4', (byte)'9', (byte)'5', (byte)'9', (byte)'6', (byte)'9', (byte)'7', (byte)'9', (byte)'8', (byte)'9', (byte)'9',
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNegative(ulong bits) => (bits >> 63) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetSignificand(ulong bits) => bits & (DoubleImplicitBit - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetExponent(ulong bits) => (int)((bits << 1) >> (DoubleSignificandBits + 1));

    // Kept at 16 bytes to make struct-return codegen friendlier than the original
    // ulong + int + int + bool shape. -1 means "no extra digit"; 0..9 are digits.
    private readonly struct DecimalResult
    {
        private readonly int _lastDigit;

        internal DecimalResult(ulong significand, int exponent, int lastDigit, bool hasLastDigit)
        {
            Significand = significand;
            Exponent = exponent;
            Debug.Assert(!hasLastDigit || (uint)lastDigit <= 9);
            _lastDigit = hasLastDigit ? lastDigit : -1;
        }

        internal ulong Significand { get; }
        internal int Exponent { get; }
        internal bool HasLastDigit => _lastDigit >= 0;
        internal int LastDigit => _lastDigit;
    }
}
