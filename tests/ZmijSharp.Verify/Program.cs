using System.Diagnostics;
using System.Globalization;
using System.Text;
using ZmijSharp;
using ZmijSharp.Verify;
using UnroundedScaling.Comparison;

var invariant = CultureInfo.InvariantCulture;
var formats = new[] { "", "G", "g", "R", "r", "G0", "g0", "R17", "r17" };

bool producerOnly = Array.Exists(args, value => value == "--producer-only");
bool unroundedSmoke = Array.Exists(args, value => value == "--unrounded-smoke");
bool formatMatrix = Array.Exists(args, value => value == "--format-matrix");
int unroundedCountArgument = Array.FindIndex(args, value => value == "--unrounded-count");
int unroundedCount = unroundedCountArgument >= 0 && unroundedCountArgument + 1 < args.Length ? int.Parse(args[unroundedCountArgument + 1], invariant) : 100_000;
CacheIdentity.Check();
Console.WriteLine("power cache identity: 618");
if (unroundedSmoke)
{
    RunUnroundedSmoke(unroundedCount);
}

if (formatMatrix)
    ExtendedVerification.RunFormatMatrix();
else if (!producerOnly)
{
    RunTargeted();
    RunRandomDoubles(100_000);
    RunRandomFloats(100_000);
    RunOracle(2_000);
    RunStructuredOracle();
    ShortestDecimalOracle.RunMutationTests();
}

int doubleSweepArgument = Array.FindIndex(args, value => value == "--double-producer");
if (doubleSweepArgument >= 0)
{
    int count = doubleSweepArgument + 1 < args.Length ? int.Parse(args[doubleSweepArgument + 1], invariant) : 100_000_000;
    RunDoubleProducerSweep(count);
}

int doubleOutputArgument = Array.FindIndex(args, value => value == "--double-output");
if (doubleOutputArgument >= 0)
{
    int count = doubleOutputArgument + 1 < args.Length ? int.Parse(args[doubleOutputArgument + 1], invariant) : 10_000_000;
    ExtendedVerification.RunDoubleOutputSweep(count);
}

int upstreamArgument = Array.FindIndex(args, value => value == "--upstream");
if (upstreamArgument >= 0)
{
    if (upstreamArgument + 1 >= args.Length)
        throw new ArgumentException("--upstream requires a native executable path.");
    string executable = args[upstreamArgument + 1];
    int count = upstreamArgument + 2 < args.Length ? int.Parse(args[upstreamArgument + 2], invariant) : 1_000_000;
    ExtendedVerification.RunUpstreamDifferential(executable, count);
}

int exhaustiveArgument = Array.FindIndex(args, value => value == "--float-exhaustive");
if (exhaustiveArgument >= 0)
{
    int shard = exhaustiveArgument + 1 < args.Length ? int.Parse(args[exhaustiveArgument + 1], invariant) : 0;
    int shards = exhaustiveArgument + 2 < args.Length ? int.Parse(args[exhaustiveArgument + 2], invariant) : 1;
    RunExhaustiveFloat(shard, shards, output: false);
}

int exhaustiveOutputArgument = Array.FindIndex(args, value => value == "--float-output-exhaustive");
if (exhaustiveOutputArgument >= 0)
{
    int shard = exhaustiveOutputArgument + 1 < args.Length ? int.Parse(args[exhaustiveOutputArgument + 1], invariant) : 0;
    int shards = exhaustiveOutputArgument + 2 < args.Length ? int.Parse(args[exhaustiveOutputArgument + 2], invariant) : 1;
    RunExhaustiveFloat(shard, shards, output: true);
}

Console.WriteLine("All comparisons passed.");

void RunTargeted()
{
    double[] doubles =
    [
        0.0, -0.0, 1, -1, 0.1, -0.1, Math.PI, Math.E,
        double.Epsilon, double.MinValue, double.MaxValue,
        FloatConstants.DoubleMinNormal, BitDecrement(FloatConstants.DoubleMinNormal),
        1e-4, 1e-5, 1e15, 1e16, 1e17,
        double.PositiveInfinity, double.NegativeInfinity, double.NaN,
    ];

    foreach (double value in doubles)
        CheckDouble(value);

    float[] floats =
    [
        0f, -0f, 1f, -1f, 0.1f, -0.1f, MathF.PI, MathF.E,
        float.Epsilon, float.MinValue, float.MaxValue,
        FloatConstants.FloatMinNormal, BitDecrement32(FloatConstants.FloatMinNormal),
        1e-4f, 1e-5f, 1e7f, 1e8f, 1e9f,
        float.PositiveInfinity, float.NegativeInfinity, float.NaN,
    ];

    foreach (float value in floats)
        CheckFloat(value);

    CheckDoubleDestinations(doubles);
    CheckFloatDestinations(floats);

    var custom = (NumberFormatInfo)invariant.NumberFormat.Clone();
    custom.NumberDecimalSeparator = "::";
    custom.NegativeSign = "NEG";
    custom.PositiveSign = "POS";
    custom.NaNSymbol = "not-a-number";
    custom.PositiveInfinitySymbol = "plus-inf";
    custom.NegativeInfinitySymbol = "minus-inf";

    foreach (double value in new[] { -0.0, -1e-10, 1.25, 1e20, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
    {
        foreach (string format in new[] { "G", "g", "R", "r" })
        {
            string expected = value.ToString(format, custom);
            string actual = ZmijFormatter.Format(value, format, custom);
            if (!StringComparer.Ordinal.Equals(expected, actual))
                Fail("double/custom-culture", value, format, expected, actual, (ulong)BitConverter.DoubleToInt64Bits(value));
        }
    }

}

void RunUnroundedSmoke(int count)
{
    ulong state = 0x6A09E667F3BCC909UL;
    Span<byte> digits = stackalloc byte[32];
    Span<byte> zmijDigits = stackalloc byte[32];
    Span<char> chars = stackalloc char[128];
    Span<byte> utf8 = stackalloc byte[128];
    Span<byte> expectedUtf8 = stackalloc byte[128];
    for (int i = 0; i < count; i++)
    {
        ulong bits = NextBits(ref state);
        CheckUnrounded(BitConverter.Int64BitsToDouble(unchecked((long)bits)), digits, zmijDigits, chars, utf8, expectedUtf8);
    }
    RunUnroundedBoundaries(digits, zmijDigits, chars, utf8, expectedUtf8);
    RunUnroundedFallbacks(chars, utf8, expectedUtf8);
    Console.WriteLine($"UnroundedScaling smoke: {count:N0} random + all binary exponent boundaries");
}

void RunUnroundedBoundaries(Span<byte> digits, Span<byte> zmijDigits, Span<char> chars, Span<byte> utf8, Span<byte> expectedUtf8)
{
    const ulong doubleFractionMask = (1UL << 52) - 1;
    for (int exponent = 0; exponent < 2047; exponent++)
    {
        ulong baseBits = (ulong)exponent << 52;
        foreach (ulong fraction in new[] { 0UL, 1UL, doubleFractionMask - 1, doubleFractionMask })
        {
            ulong bits = baseBits | fraction;
            CheckUnrounded(BitConverter.Int64BitsToDouble(unchecked((long)bits)), digits, zmijDigits, chars, utf8, expectedUtf8);
            CheckUnrounded(BitConverter.Int64BitsToDouble(unchecked((long)(bits | (1UL << 63)))), digits, zmijDigits, chars, utf8, expectedUtf8);
        }
    }
}

void RunUnroundedFallbacks(Span<char> chars, Span<byte> utf8, Span<byte> expectedUtf8)
{
    // The comparison-only UnroundedScaling formatter supports the shortest
    // path natively; everything else must fall back to the runtime with
    // byte-identical output. Spot-check the fallback contracts once.
    double[] values = [0.0, -0.0, -1.0, 1.25, 1e20, double.NaN, double.PositiveInfinity, double.NegativeInfinity];
    string[] formats = ["G5", "F", "E"];
    foreach (double value in values)
    {
        foreach (string format in formats)
        {
            string expected = value.ToString(format, invariant);
            if (!UnroundedScalingFormatter.TryFormat(value, chars, out int cw, format, invariant))
                throw new Exception($"UnroundedScaling formatter unexpectedly failed for {value:R} '{format}'.");
            if (!StringComparer.Ordinal.Equals(expected, new string(chars[..cw])))
                throw new Exception($"UnroundedScaling fallback mismatch: value={value:R}, format={format}, expected='{expected}', actual='{new string(chars[..cw])}'.");

            if (!value.TryFormat(expectedUtf8, out int ew, format, invariant))
                throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");
            if (!UnroundedScalingFormatter.TryFormatUtf8(value, utf8, out int bw, format))
                throw new Exception($"UnroundedScaling UTF-8 formatter unexpectedly failed for {value:R} '{format}'.");
            if (!expectedUtf8[..ew].SequenceEqual(utf8[..bw]))
                throw new Exception($"UnroundedScaling UTF-8 fallback mismatch: value={value:R}, format={format}.");
        }
    }
    Console.WriteLine("UnroundedScaling fallbacks: counted/unsupported/non-finite match runtime");
}

void CheckUnrounded(double value, Span<byte> digits, Span<byte> zmijDigits, Span<char> chars, Span<byte> utf8, Span<byte> expectedUtf8)
{
    bool success = UnroundedScalingDigits.TryGetSignificantDigits(value, digits, out int digitCount, out int scale);
    if (!double.IsFinite(value) || value == 0)
    {
        // The comparison wrapper rejects what the runtime handles elsewhere;
        // the decompose bench only measures finite non-zero inputs.
        if (success)
            throw new Exception($"UnroundedScaling accepted a non-finite or zero value: {value}.");
        return;
    }

    if (!success)
        throw new Exception($"UnroundedScaling rejected a finite non-zero value: {value:R}.");

    // Exact agreement with the Zmij decomposition: same digits and same scale
    // means both decompositions timed in the benchmarks compute the identical
    // shortest decomposition, so the speed comparison is apples-to-apples.
    if (!ZmijCore.TryGetSignificantDigits(value, zmijDigits, out int zmijCount, out int zmijScale, out _))
        throw new Exception("Zmij decomposition unexpectedly failed.");
    if (zmijCount != digitCount || zmijScale != scale || !digits[..digitCount].SequenceEqual(zmijDigits[..zmijCount]))
        throw new Exception($"UnroundedScaling/Zmij mismatch: value={value:R}, unrounded='{Encoding.ASCII.GetString(digits[..digitCount])}'@{scale}, zmij='{Encoding.ASCII.GetString(zmijDigits[..zmijCount])}'@{zmijScale}.");

    string text = (value < 0 ? "-" : string.Empty) + ((char)digits[0]);
    if (digitCount > 1)
        text += "." + Encoding.ASCII.GetString(digits.Slice(1, digitCount - 1));
    text += "E" + (scale - 1).ToString(invariant);
    if (!double.TryParse(text, NumberStyles.Float, invariant, out double roundTrip) || !BitConverter.DoubleToInt64Bits(roundTrip).Equals(BitConverter.DoubleToInt64Bits(value)))
        throw new Exception($"UnroundedScaling round-trip failure: bits=0x{(ulong)BitConverter.DoubleToInt64Bits(value):X16}, text={text}, expected={value.ToString("R", invariant)}, roundtrip=0x{BitConverter.DoubleToInt64Bits(roundTrip):X16}");

    // Formatted-output identity through the comparison-only formatter:
    // char output must match Zmij and the runtime exactly; UTF-8 output must
    // match the runtime exactly. No formatted timing may quote these paths
    // until this gate passes.
    string expectedString = value.ToString("", invariant);
    if (!StringComparer.Ordinal.Equals(expectedString, ZmijFormatter.Format(value, "", invariant)))
        throw new Exception($"Zmij/runtime mismatch (tripwire): value={value:R}.");
    if (!UnroundedScalingFormatter.TryFormat(value, chars, out int cw, default, invariant))
        throw new Exception($"UnroundedScaling formatter unexpectedly failed for {value:R}.");
    if (!StringComparer.Ordinal.Equals(expectedString, new string(chars[..cw])))
        throw new Exception($"UnroundedScaling formatted mismatch: value={value:R}, expected='{expectedString}', actual='{new string(chars[..cw])}'.");

    if (!value.TryFormat(expectedUtf8, out int ew, default, invariant))
        throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");
    if (!UnroundedScalingFormatter.TryFormatUtf8(value, utf8, out int bw))
        throw new Exception($"UnroundedScaling UTF-8 formatter unexpectedly failed for {value:R}.");
    if (!expectedUtf8[..ew].SequenceEqual(utf8[..bw]))
        throw new Exception($"UnroundedScaling UTF-8 mismatch: value={value:R}.");
}

void RunRandomDoubles(int count)
{
    ulong state = 0x5A17_1234_5678_9ABCUL;
    for (int i = 0; i < count; i++)
    {
        ulong bits = NextBits(ref state);
        CheckDouble(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
    }
    Console.WriteLine($"double random: {count:N0}");
}

void RunRandomFloats(int count)
{
    ulong state = 0xF10A_700D_CAFE_BEEFUL;
    for (int i = 0; i < count; i++)
    {
        uint bits = (uint)NextBits(ref state);
        CheckFloat(BitConverter.Int32BitsToSingle(unchecked((int)bits)));
    }
    Console.WriteLine($"float random: {count:N0}");
}

void RunStructuredOracle()
{
    const ulong doubleFractionMask = (1UL << 52) - 1;
    const uint floatFractionMask = (1u << 23) - 1;
    for (int exponent = 0; exponent < 2047; exponent++)
    {
        ulong baseBits = (ulong)exponent << 52;
        foreach (ulong fraction in new[] { 0UL, 1UL, doubleFractionMask - 1, doubleFractionMask })
        {
            ulong bits = baseBits | fraction;
            ShortestDecimalOracle.VerifyDouble(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
            ShortestDecimalOracle.VerifyDouble(BitConverter.Int64BitsToDouble(unchecked((long)(bits | (1UL << 63)))));
        }
    }

    for (int exponent = 0; exponent < 255; exponent++)
    {
        uint baseBits = (uint)exponent << 23;
        foreach (uint fraction in new[] { 0u, 1u, floatFractionMask - 1, floatFractionMask })
        {
            uint bits = baseBits | fraction;
            ShortestDecimalOracle.VerifyFloat(BitConverter.Int32BitsToSingle(unchecked((int)bits)));
            ShortestDecimalOracle.VerifyFloat(BitConverter.Int32BitsToSingle(unchecked((int)(bits | (1u << 31)))));
        }
    }
    foreach (uint bits in new[] { 0x22433EDAU, 0x4A36E431U })
        ShortestDecimalOracle.VerifyFloat(BitConverter.Int32BitsToSingle(unchecked((int)bits)));

    Console.WriteLine("structured oracle: all binary exponent boundaries");
}

void RunOracle(int count)
{
    ulong doubleState = 0x0A11_CE00_D15C_A11EUL;
    ulong floatState = 0x0F10_7A11_5EED_1234UL;
    for (int i = 0; i < count; i++)
    {
        double doubleValue = BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref doubleState)));
        if (double.IsFinite(doubleValue) && doubleValue != 0)
            ShortestDecimalOracle.VerifyDouble(doubleValue);

        float floatValue = BitConverter.Int32BitsToSingle(unchecked((int)(uint)NextBits(ref floatState)));
        if (float.IsFinite(floatValue) && floatValue != 0)
            ShortestDecimalOracle.VerifyFloat(floatValue);
    }
    Console.WriteLine($"exact oracle: {count:N0} double + {count:N0} float");
}

static ulong NextBits(ref ulong state)
{
    state += 0x9E3779B97F4A7C15UL;
    ulong z = state;
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
    return z ^ (z >> 31);
}

void RunDoubleProducerSweep(int count)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    ulong state = 0xD0B1E5A7C0DE1234UL;
    for (int i = 0; i < count; i++)
    {
        double value = BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref state)));
        CheckDoubleProducerOnly(value);
    }
    stopwatch.Stop();
    double seconds = stopwatch.Elapsed.TotalSeconds;
    Console.WriteLine($"double producer-only sweep: {count:N0} values, {seconds:N3}s, {count / seconds:N0} values/s");
}

void RunExhaustiveFloat(int shard, int shards, bool output)
{
    if (shards <= 0 || shard < 0 || shard >= shards)
        throw new ArgumentOutOfRangeException(nameof(shard));

    Stopwatch stopwatch = Stopwatch.StartNew();
    ulong total = 1UL << 32;
    ulong start = total * (ulong)shard / (ulong)shards;
    ulong end = total * (ulong)(shard + 1) / (ulong)shards;

    for (ulong u = start; u < end; u++)
    {
        float value = BitConverter.Int32BitsToSingle(unchecked((int)(uint)u));
        if (output)
            ExtendedVerification.CheckFloatOutput(value, (uint)u);
        else
            CheckFloatProducerOnly(value);
    }
    stopwatch.Stop();
    double seconds = stopwatch.Elapsed.TotalSeconds;
    double valuesPerSecond = (end - start) / seconds;
    Console.WriteLine($"float {(output ? "output" : "producer-only")} exhaustive shard {shard}/{shards}: [{start:X8}, {end:X8}), {seconds:N3}s, {valuesPerSecond:N0} values/s");
}

void CheckDouble(double value)
{
    Span<char> chars = stackalloc char[64];
    Span<byte> utf8 = stackalloc byte[64];
    Span<byte> expectedUtf8 = stackalloc byte[128];

    foreach (string format in formats)
    {
        string expected = value.ToString(format, invariant);
        string actual = ZmijFormatter.Format(value, format, invariant);
        if (!StringComparer.Ordinal.Equals(expected, actual))
            Fail("double/string", value, format, expected, actual, (ulong)BitConverter.DoubleToInt64Bits(value));

        if (!ZmijFormatter.TryFormat(value, chars, out int cw, format, invariant))
            throw new Exception("TryFormat unexpectedly failed.");
        actual = new string(chars[..cw]);
        if (!StringComparer.Ordinal.Equals(expected, actual))
            Fail("double/char", value, format, expected, actual, (ulong)BitConverter.DoubleToInt64Bits(value));

        if (!value.TryFormat(expectedUtf8, out int ew, format, invariant))
            throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");
        if (!ZmijFormatter.TryFormatUtf8(value, utf8, out int bw, format))
            throw new Exception("TryFormatUtf8 unexpectedly failed.");
        if (!expectedUtf8[..ew].SequenceEqual(utf8[..bw]))
            Fail("double/utf8", value, format, Encoding.UTF8.GetString(expectedUtf8[..ew]), Encoding.UTF8.GetString(utf8[..bw]), (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    CheckProducer(value);
}

void CheckFloat(float value)
{
    Span<char> chars = stackalloc char[48];
    Span<byte> utf8 = stackalloc byte[48];
    Span<byte> expectedUtf8 = stackalloc byte[128];

    foreach (string format in formats)
    {
        string expected = value.ToString(format, invariant);
        string actual = ZmijFormatter.Format(value, format, invariant);
        if (!StringComparer.Ordinal.Equals(expected, actual))
            Fail("float/string", value, format, expected, actual, (uint)BitConverter.SingleToInt32Bits(value));

        if (!ZmijFormatter.TryFormat(value, chars, out int cw, format, invariant))
            throw new Exception("TryFormat unexpectedly failed.");
        actual = new string(chars[..cw]);
        if (!StringComparer.Ordinal.Equals(expected, actual))
            Fail("float/char", value, format, expected, actual, (uint)BitConverter.SingleToInt32Bits(value));

        if (!value.TryFormat(expectedUtf8, out int ew, format, invariant))
            throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");
        if (!ZmijFormatter.TryFormatUtf8(value, utf8, out int bw, format))
            throw new Exception("TryFormatUtf8 unexpectedly failed.");
        if (!expectedUtf8[..ew].SequenceEqual(utf8[..bw]))
            Fail("float/utf8", value, format, Encoding.UTF8.GetString(expectedUtf8[..ew]), Encoding.UTF8.GetString(utf8[..bw]), (uint)BitConverter.SingleToInt32Bits(value));
    }

    CheckFloatProducer(value);
}

void CheckProducer(double value)
{
    if (!double.IsFinite(value))
    {
        if (ZmijCore.TryGetSignificantDigits(value, stackalloc byte[20], out _, out _, out _))
            throw new Exception("Producer accepted a non-finite value.");
        return;
    }

    Span<byte> digits = stackalloc byte[20];
    ZmijDecimal expected = ZmijFormatter.ToDecimal(value);
    bool success = ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative);
    string actual = Encoding.ASCII.GetString(digits[..digitCount]);
    string expectedDigits = expected.Significand == 0 ? "" : expected.Significand.ToString(invariant);
    if (!success || !StringComparer.Ordinal.Equals(actual, expectedDigits) || scale != digitCount + expected.Exponent || negative != expected.IsNegative)
        Fail("double/producer", value, "", expectedDigits, actual, (ulong)BitConverter.DoubleToInt64Bits(value));
}

void CheckFloatProducer(float value)
{
    if (!float.IsFinite(value))
    {
        if (ZmijCore.TryGetSignificantDigits(value, stackalloc byte[20], out _, out _, out _))
            throw new Exception("Producer accepted a non-finite value.");
        return;
    }

    Span<byte> digits = stackalloc byte[20];
    ZmijDecimal expected = ZmijFormatter.ToDecimal(value);
    bool success = ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative);
    string actual = Encoding.ASCII.GetString(digits[..digitCount]);
    string expectedDigits = expected.Significand == 0 ? "" : expected.Significand.ToString(invariant);
    if (!success || !StringComparer.Ordinal.Equals(actual, expectedDigits) || scale != digitCount + expected.Exponent || negative != expected.IsNegative)
        Fail("float/producer", value, "", expectedDigits, actual, (uint)BitConverter.SingleToInt32Bits(value));
}

void CheckDoubleProducerOnly(double value)
{
    Span<byte> digits = stackalloc byte[20];
    bool success = ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative);
    if (!double.IsFinite(value))
    {
        if (success)
            throw new Exception("Producer accepted a non-finite value.");
        return;
    }

    if (!success || digitCount < 0 || digitCount > 17 || scale < -323 || scale > 309)
        throw new Exception($"Invalid producer result for bits 0x{(ulong)BitConverter.DoubleToInt64Bits(value):X16}.");
    if (value == 0)
    {
        // Check the sign of both zeros explicitly: shortest nonzero output carries at
        // most 17 digits, and zero carries none.
        if (digitCount != 0)
            throw new Exception("Producer emitted digits for zero.");
        if (negative != ((BitConverter.DoubleToInt64Bits(value) & unchecked((long)0x8000000000000000UL)) != 0))
            throw new Exception("Producer sign mismatch for zero.");
        return;
    }
    if (digitCount == 0)
        throw new Exception("Producer emitted no digits for a nonzero value.");
    if (negative != ((BitConverter.DoubleToInt64Bits(value) & unchecked((long)0x8000000000000000UL)) != 0))
        throw new Exception("Producer sign mismatch.");
    for (int i = 0; i < digitCount; i++)
    {
        if (digits[i] < (byte)'0' || digits[i] > (byte)'9')
            throw new Exception("Producer emitted a non-decimal digit.");
    }
}

void CheckFloatProducerOnly(float value)
{
    Span<byte> digits = stackalloc byte[20];
    bool success = ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative);
    if (!float.IsFinite(value))
    {
        if (success)
            throw new Exception("Producer accepted a non-finite value.");
        return;
    }

    if (!success || digitCount < 0 || digitCount > 9 || scale < -44 || scale > 39)
        throw new Exception($"Invalid producer result for bits 0x{(uint)BitConverter.SingleToInt32Bits(value):X8}.");
    if (value == 0)
    {
        // Check the sign of both zeros explicitly: shortest nonzero output carries at
        // most 9 digits, and zero carries none.
        if (digitCount != 0)
            throw new Exception("Producer emitted digits for zero.");
        if (negative != ((BitConverter.SingleToInt32Bits(value) & unchecked((int)0x80000000)) != 0))
            throw new Exception("Producer sign mismatch for zero.");
        return;
    }
    if (digitCount == 0)
        throw new Exception("Producer emitted no digits for a nonzero value.");
    if (negative != ((BitConverter.SingleToInt32Bits(value) & unchecked((int)0x80000000)) != 0))
        throw new Exception("Producer sign mismatch.");
    for (int i = 0; i < digitCount; i++)
    {
        if (digits[i] < (byte)'0' || digits[i] > (byte)'9')
            throw new Exception("Producer emitted a non-decimal digit.");
    }
}

void CheckDoubleDestinations(ReadOnlySpan<double> values)
{
    char[] chars = new char[128];
    byte[] bytes = new byte[128];
    Span<char> expectedChars = stackalloc char[128];
    Span<byte> expectedBytes = stackalloc byte[128];
    int cw;
    int bw;

    foreach (double value in values)
    {
        foreach (string format in new[] { "", "G", "R" })
        {
            if (!value.TryFormat(expectedChars, out int ec, format, invariant))
                throw new Exception("Runtime TryFormat unexpectedly failed.");

            for (int capacity = 0; capacity < ec; capacity++)
            {
                Array.Fill(chars, '~');
                if (ZmijFormatter.TryFormat(value, chars.AsSpan(0, capacity), out cw, format, invariant) || cw != 0 || chars.Any(c => c != '~'))
                    throw new Exception("TryFormat modified a failed destination.");
            }

            Array.Fill(chars, '~');
            if (!ZmijFormatter.TryFormat(value, chars.AsSpan(0, ec), out cw, format, invariant) || !chars.AsSpan(0, ec).SequenceEqual(expectedChars[..ec]))
                throw new Exception("TryFormat exact destination mismatch.");

            if (!value.TryFormat(expectedBytes, out int eb, format, invariant))
                throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");

            for (int capacity = 0; capacity < eb; capacity++)
            {
                Array.Fill(bytes, (byte)0xA5);
                if (ZmijFormatter.TryFormatUtf8(value, bytes.AsSpan(0, capacity), out bw, format) || bw != 0 || bytes.Any(b => b != 0xA5))
                    throw new Exception("TryFormatUtf8 modified a failed destination.");
            }

            Array.Fill(bytes, (byte)0xA5);
            if (!ZmijFormatter.TryFormatUtf8(value, bytes.AsSpan(0, eb), out bw, format) || !bytes.AsSpan(0, eb).SequenceEqual(expectedBytes[..eb]))
                throw new Exception("TryFormatUtf8 exact destination mismatch.");
        }
    }
}

void CheckFloatDestinations(ReadOnlySpan<float> values)
{
    char[] chars = new char[128];
    byte[] bytes = new byte[128];
    Span<char> expectedChars = stackalloc char[128];
    Span<byte> expectedBytes = stackalloc byte[128];
    int cw;
    int bw;

    foreach (float value in values)
    {
        foreach (string format in new[] { "", "G", "R" })
        {
            if (!value.TryFormat(expectedChars, out int ec, format, invariant))
                throw new Exception("Runtime TryFormat unexpectedly failed.");

            for (int capacity = 0; capacity < ec; capacity++)
            {
                Array.Fill(chars, '~');
                if (ZmijFormatter.TryFormat(value, chars.AsSpan(0, capacity), out cw, format, invariant) || cw != 0 || chars.Any(c => c != '~'))
                    throw new Exception("TryFormat modified a failed destination.");
            }

            Array.Fill(chars, '~');
            if (!ZmijFormatter.TryFormat(value, chars.AsSpan(0, ec), out cw, format, invariant) || !chars.AsSpan(0, ec).SequenceEqual(expectedChars[..ec]))
                throw new Exception("TryFormat exact destination mismatch.");

            if (!value.TryFormat(expectedBytes, out int eb, format, invariant))
                throw new Exception("Runtime UTF-8 TryFormat unexpectedly failed.");

            for (int capacity = 0; capacity < eb; capacity++)
            {
                Array.Fill(bytes, (byte)0xA5);
                if (ZmijFormatter.TryFormatUtf8(value, bytes.AsSpan(0, capacity), out bw, format) || bw != 0 || bytes.Any(b => b != 0xA5))
                    throw new Exception("TryFormatUtf8 modified a failed destination.");
            }

            Array.Fill(bytes, (byte)0xA5);
            if (!ZmijFormatter.TryFormatUtf8(value, bytes.AsSpan(0, eb), out bw, format) || !bytes.AsSpan(0, eb).SequenceEqual(expectedBytes[..eb]))
                throw new Exception("TryFormatUtf8 exact destination mismatch.");
        }
    }
}

static void Fail<T>(string kind, T value, string format, string expected, string actual, ulong bits)
{
    throw new Exception($"Mismatch {kind}: bits=0x{bits:X16}, value={value}, format={format}, expected='{expected}', actual='{actual}'");
}

static double BitDecrement(double value) => Math.BitDecrement(value);
static float BitDecrement32(float value) => MathF.BitDecrement(value);

static class FloatConstants
{
    // Avoid relying on framework field availability in test source.
    internal static double DoubleMinNormal => BitConverter.Int64BitsToDouble(0x0010_0000_0000_0000);
    internal static float FloatMinNormal => BitConverter.Int32BitsToSingle(0x0080_0000);
}
