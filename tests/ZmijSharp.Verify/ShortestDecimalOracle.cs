using System.Numerics;
using System.Text;
using ZmijSharp;

namespace ZmijSharp.Verify;

internal static class ShortestDecimalOracle
{
    internal static void RunMutationTests()
    {
        float value = BitConverter.Int32BitsToSingle(unchecked((int)0x22433EDA));
        Span<byte> digits = stackalloc byte[20];
        if (!ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out _))
            throw new InvalidOperationException("Mutation setup failed.");
        BigInteger integer = ParseDigits(digits[..digitCount]);
        int exponent = scale - digitCount;
        BigInteger numerator = integer;
        BigInteger denominator = BigInteger.One;
        if (exponent >= 0)
            numerator *= Power10(exponent);
        else
            denominator = Power10(-exponent);
        Interval interval = GetInterval(0x22433EDAU, 23, 8, 127, -149);
        string digitText = System.Text.Encoding.ASCII.GetString(digits[..digitCount]);

        ExpectFailure(() => VerifyCandidate(0x22433EDAU, integer, exponent, numerator, denominator, interval, digitText, digitCount, scale, true, true), "wrong sign");
        ExpectFailure(() => VerifyCandidate(0x22433EDAU, integer * 10, exponent - 1, numerator, denominator, interval, digitText + "0", digitCount + 1, scale, false, true), "extra digit");
        if (digitCount > 1)
            ExpectFailure(() => VerifyCandidate(0x22433EDAU, integer / 10, exponent + 1, numerator, denominator, interval, digitText[..^1], digitCount - 1, scale - 1, false, true), "shorter digit");
        ExpectFailure(() => VerifyCandidate(0x22433EDAU, integer + 1, exponent, numerator, denominator, interval, digitText, digitCount, scale, false, true), "boundary candidate");
        Console.WriteLine("exact oracle mutations: rejected");
    }

    private static void ExpectFailure(Action action, string name)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException($"Oracle mutation was not rejected: {name}.");
    }

    internal static void VerifyDouble(double value)
    {
        if (!double.IsFinite(value) || value == 0)
            return;

        Span<byte> digits = stackalloc byte[20];
        if (!ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative))
            throw new InvalidOperationException("The double producer rejected a finite value.");

        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        BigInteger candidateNumerator = ParseDigits(digits[..digitCount]);
        BigInteger candidateDenominator = BigInteger.One;
        int candidateExponent = scale - digitCount;
        if (candidateExponent >= 0)
            candidateNumerator *= Power10(candidateExponent);
        else
            candidateDenominator = Power10(-candidateExponent);
        Interval interval = GetInterval(bits & 0x7fff_ffff_ffff_ffffUL, 52, 11, 1023, -1074);
        VerifyCandidate(bits, ParseDigits(digits[..digitCount]), candidateExponent, candidateNumerator, candidateDenominator, interval, Encoding.ASCII.GetString(digits[..digitCount]), digitCount, scale, negative, isFloat: false);
    }

    internal static void VerifyFloat(float value)
    {
        if (!float.IsFinite(value) || value == 0)
            return;

        Span<byte> digits = stackalloc byte[20];
        if (!ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative))
            throw new InvalidOperationException("The float producer rejected a finite value.");

        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        BigInteger candidateNumerator = ParseDigits(digits[..digitCount]);
        BigInteger candidateDenominator = BigInteger.One;
        int candidateExponent = scale - digitCount;
        if (candidateExponent >= 0)
            candidateNumerator *= Power10(candidateExponent);
        else
            candidateDenominator = Power10(-candidateExponent);
        Interval interval = GetInterval(bits & 0x7fffffffUL, 23, 8, 127, -149);
        VerifyCandidate(bits, ParseDigits(digits[..digitCount]), candidateExponent, candidateNumerator, candidateDenominator, interval, Encoding.ASCII.GetString(digits[..digitCount]), digitCount, scale, negative, isFloat: true);
    }

    private static void VerifyCandidate(ulong originalBits, BigInteger candidateInteger, int candidateExponent,
        BigInteger candidateNumerator, BigInteger candidateDenominator, Interval interval, string digits, int digitCount, int scale, bool negative, bool isFloat)
    {
        bool even = (originalBits & 1) == 0;
        int lowerComparison = Compare(candidateNumerator, candidateDenominator, interval.Lower);
        int upperComparison = Compare(candidateNumerator, candidateDenominator, interval.Upper);
        if (lowerComparison < 0 || (lowerComparison == 0 && !even) || upperComparison > 0 || (upperComparison == 0 && !even))
            throw new InvalidOperationException($"Candidate is outside the exact rounding interval: bits={originalBits:X16}, digits={digits}, scale={scale}.");

        ulong signMask = isFloat ? 0x80000000UL : 0x8000000000000000UL;
        if (negative != ((originalBits & signMask) != 0))
            throw new InvalidOperationException("Candidate sign does not match the input.");

        ulong exactBits = RoundToBits(candidateNumerator, candidateDenominator, isFloat);
        ulong absoluteMask = isFloat ? 0x7fffffffUL : 0x7fffffffffffffffUL;
        if (exactBits != (originalBits & absoluteMask))
            throw new InvalidOperationException("Candidate did not round-trip through exact decimal-to-binary arithmetic.");

        VerifyCanonical(candidateInteger, candidateExponent, digitCount, interval, originalBits, digits);

        int leadingExponent = scale - 1;
        for (int shorterDigits = 1; shorterDigits < digitCount; shorterDigits++)
        {
            for (int exponent = leadingExponent - shorterDigits + 1; exponent <= leadingExponent; exponent++)
            {
                BigInteger min = Power10(shorterDigits - 1);
                BigInteger max = Power10(shorterDigits) - 1;
                if (HasIntegerInInterval(interval, exponent, min, max, even))
                    throw new InvalidOperationException($"Candidate is not shortest: bits={originalBits:X8}, digits={digits}, scale={scale}, shorter={shorterDigits}, exponent={exponent}.");
            }
        }
    }

    private static void VerifyCanonical(BigInteger candidateInteger, int candidateExponent, int digitCount,
        Interval interval, ulong originalBits, string digits)
    {
        BigInteger scaledNumerator = interval.Value.Numerator;
        BigInteger scaledDenominator = interval.Value.Denominator;
        if (candidateExponent >= 0)
            scaledDenominator *= Power10(candidateExponent);
        else
            scaledNumerator *= Power10(-candidateExponent);

        BigInteger floor = FloorDivide(scaledNumerator, scaledDenominator);
        BigInteger best = BigInteger.MinusOne;
        BigInteger bestDistance = BigInteger.MinusOne;
        for (BigInteger value = floor - 2; value <= floor + 2; value++)
        {
            if (value < Power10(digitCount - 1) || value > Power10(digitCount) - 1 || !IsInInterval(value, candidateExponent, interval, (originalBits & 1) == 0))
                continue;
            BigInteger distance = BigInteger.Abs(scaledNumerator - (value * scaledDenominator));
            if (best < 0 || distance < bestDistance || (distance == bestDistance && (value.IsEven && !best.IsEven || value == best)))
            {
                best = value;
                bestDistance = distance;
            }
        }
        if (best != candidateInteger)
            throw new InvalidOperationException($"Candidate is not canonical: bits={originalBits:X16}, digits={digits}, expected={best}, actual={candidateInteger}.");
    }

    private static bool IsInInterval(BigInteger integer, int exponent, Interval interval, bool even)
    {
        BigInteger valueFactor = exponent >= 0 ? Power10(exponent) : BigInteger.One;
        BigInteger intervalFactor = exponent < 0 ? Power10(-exponent) : BigInteger.One;
        BigInteger valueNumerator = integer * valueFactor;
        BigInteger lowerNumerator = interval.Lower.Numerator * intervalFactor;
        BigInteger upperNumerator = interval.Upper.Numerator * intervalFactor;
        int lowerComparison = (valueNumerator * interval.Lower.Denominator).CompareTo(lowerNumerator);
        int upperComparison = (valueNumerator * interval.Upper.Denominator).CompareTo(upperNumerator);
        if (lowerComparison == 0 && !even || upperComparison == 0 && !even)
            return false;
        return lowerComparison >= 0 && upperComparison <= 0;
    }

    private static ulong RoundToBits(BigInteger numerator, BigInteger denominator, bool isFloat)
    {
        int significandBits = isFloat ? 23 : 52;
        int bias = isFloat ? 127 : 1023;
        int minNormal = isFloat ? -126 : -1022;
        int minSubnormal = isFloat ? -149 : -1074;
        int maxExponent = isFloat ? 127 : 1023;
        int exponent = FindExponent(numerator, denominator, minSubnormal, maxExponent);
        BigInteger significand;
        int storedExponent;
        if (exponent < minNormal)
        {
            significand = RoundScaled(numerator, denominator, -minSubnormal);
            storedExponent = 0;
            if (significand >= BigInteger.One << significandBits)
            {
                significand = BigInteger.Zero;
                storedExponent = 1;
            }
        }
        else
        {
            significand = RoundScaled(numerator, denominator, significandBits - exponent);
            if (significand >= BigInteger.One << (significandBits + 1))
            {
                significand >>= 1;
                exponent++;
            }
            storedExponent = exponent + bias;
        }

        ulong fractionMask = (1UL << significandBits) - 1;
        return ((ulong)storedExponent << significandBits) | (ulong)significand & fractionMask;
    }

    private static int FindExponent(BigInteger numerator, BigInteger denominator, int minExponent, int maxExponent)
    {
        int low = minExponent;
        int high = maxExponent + 1;
        while (low + 1 < high)
        {
            int middle = low + ((high - low) / 2);
            int comparison = middle >= 0
                ? numerator.CompareTo(denominator << middle)
                : (numerator << -middle).CompareTo(denominator);
            if (comparison >= 0)
                low = middle;
            else
                high = middle;
        }
        return low;
    }

    private static BigInteger RoundScaled(BigInteger numerator, BigInteger denominator, int shift)
    {
        if (shift >= 0)
            numerator <<= shift;
        else
            denominator <<= -shift;
        BigInteger result = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        BigInteger twiceRemainder = remainder << 1;
        if (twiceRemainder > denominator || (twiceRemainder == denominator && !result.IsEven))
            result++;
        return result;
    }

    private static bool HasIntegerInInterval(Interval interval, int exponent, BigInteger min, BigInteger max, bool even)
    {
        BigInteger numeratorFactor = exponent >= 0 ? BigInteger.One : Power10(-exponent);
        BigInteger denominatorFactor = exponent >= 0 ? Power10(exponent) : BigInteger.One;
        BigInteger lowerNumerator = interval.Lower.Numerator * numeratorFactor;
        BigInteger lowerDenominator = interval.Lower.Denominator * denominatorFactor;
        BigInteger upperNumerator = interval.Upper.Numerator * numeratorFactor;
        BigInteger upperDenominator = interval.Upper.Denominator * denominatorFactor;
        BigInteger lower = CeilingDivide(lowerNumerator, lowerDenominator);
        BigInteger upper = FloorDivide(upperNumerator, upperDenominator);
        if (!even && upperNumerator % upperDenominator == 0)
            upper--;
        if (!even && lowerNumerator % lowerDenominator == 0)
            lower++;
        if (lower < min)
            lower = min;
        if (upper > max)
            upper = max;
        return lower <= upper;
    }

    private static Interval GetInterval(ulong absoluteBits, int significandBits, int exponentBits, int bias, int minExponent)
    {
        Rational current = Decode(absoluteBits, significandBits, exponentBits, bias, minExponent);
        Rational lower = absoluteBits == 1
            ? Midpoint(new(BigInteger.Zero, BigInteger.One), current)
            : Midpoint(current, Decode(absoluteBits - 1, significandBits, exponentBits, bias, minExponent));
        Rational upper;
        if (absoluteBits == MaxFiniteBits(significandBits, exponentBits))
        {
            int ulpExponent = ExponentOf(absoluteBits, significandBits, exponentBits, bias, minExponent) - significandBits;
            if (ulpExponent < 0)
                throw new InvalidOperationException($"Invalid ULP exponent {ulpExponent} for bits {absoluteBits:X}.");
            BigInteger ulpNumerator = Power2(ulpExponent);
            upper = new((current.Numerator * 2) + (ulpNumerator * current.Denominator), current.Denominator * 2);
        }
        else
        {
            upper = Midpoint(current, Decode(absoluteBits + 1, significandBits, exponentBits, bias, minExponent));
        }
        return new(lower, upper, current);
    }

    private static Rational Decode(ulong bits, int significandBits, int exponentBits, int bias, int minExponent)
    {
        ulong fractionMask = (1UL << significandBits) - 1;
        int exponent = (int)(bits >> significandBits);
        ulong significand = exponent == 0 ? bits & fractionMask : fractionMask + 1 | bits & fractionMask;
        int binaryExponent = exponent == 0 ? minExponent : exponent - bias - significandBits;
        if (binaryExponent >= 0)
            return new((BigInteger)significand << binaryExponent, BigInteger.One);
        return new((BigInteger)significand, BigInteger.One << -binaryExponent);
    }

    private static Rational Midpoint(Rational left, Rational right)
        => new((left.Numerator * right.Denominator) + (right.Numerator * left.Denominator), left.Denominator * right.Denominator * 2);

    private static int ExponentOf(ulong bits, int significandBits, int exponentBits, int bias, int minExponent)
    {
        int exponent = (int)(bits >> significandBits);
        return exponent == 0 ? minExponent : exponent - bias;
    }

    private static ulong MaxFiniteBits(int significandBits, int exponentBits)
        => ((ulong)((1 << exponentBits) - 2)) << significandBits | ((1UL << significandBits) - 1);

    private static int Compare(BigInteger numerator, BigInteger denominator, Rational rational)
        => (numerator * rational.Denominator).CompareTo(rational.Numerator * denominator);

    private static BigInteger ParseDigits(ReadOnlySpan<byte> digits)
    {
        BigInteger result = BigInteger.Zero;
        foreach (byte digit in digits)
            result = result * 10 + digit - '0';
        return result;
    }

    private static BigInteger Power10(int exponent)
    {
        if (exponent < 0)
            throw new ArgumentOutOfRangeException(nameof(exponent));
        return BigInteger.Pow(10, exponent);
    }

    private static BigInteger Power2(int exponent)
    {
        if (exponent < 0)
            throw new ArgumentOutOfRangeException(nameof(exponent));
        return BigInteger.One << exponent;
    }

    private static BigInteger FloorDivide(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }

    private static BigInteger CeilingDivide(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        return remainder.Sign == 0 ? quotient : quotient + 1;
    }

    private readonly struct Rational
    {
        internal Rational(BigInteger numerator, BigInteger denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        internal BigInteger Numerator { get; }
        internal BigInteger Denominator { get; }
    }

    private readonly struct Interval
    {
        internal Interval(Rational lower, Rational upper, Rational value)
        {
            Lower = lower;
            Upper = upper;
            Value = value;
        }

        internal Rational Lower { get; }
        internal Rational Upper { get; }
        internal Rational Value { get; }
    }
}
