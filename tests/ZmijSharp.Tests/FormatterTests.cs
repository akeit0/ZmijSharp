using System.Globalization;
using System.Text;
using ZmijSharp;

namespace ZmijSharp.Tests;

public class FormatterTests
{
    private static readonly double[] DoubleValues =
    [
        0.0, -0.0, 1.0, -1.0, 0.1, -0.1, Math.PI, Math.E,
        double.Epsilon, double.MinValue, double.MaxValue,
        1e-4, 1e-5, 1e15, 1e16, 1e17,
        double.PositiveInfinity, double.NegativeInfinity, double.NaN,
    ];

    private static readonly float[] FloatValues =
    [
        0.0f, -0.0f, 1.0f, -1.0f, 0.1f, -0.1f, MathF.PI, MathF.E,
        float.Epsilon, float.MinValue, float.MaxValue,
        1e-4f, 1e-5f, 1e7f, 1e8f, 1e9f,
        float.PositiveInfinity, float.NegativeInfinity, float.NaN,
    ];

    [Test]
    public async Task DoubleFormatMatchesRuntime()
    {
        foreach (double value in DoubleValues)
        {
            string expected = value.ToString("R", CultureInfo.InvariantCulture);
            string actual = ZmijFormatter.Format(value, "R", CultureInfo.InvariantCulture);
            await Assert.That(actual).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task FloatFormatMatchesRuntime()
    {
        foreach (float value in FloatValues)
        {
            string expected = value.ToString("R", CultureInfo.InvariantCulture);
            string actual = ZmijFormatter.Format(value, "R", CultureInfo.InvariantCulture);
            await Assert.That(actual).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task DoubleSpanFormattingMatchesRuntime()
    {
        char[] expectedChars = new char[128];
        byte[] expectedBytes = new byte[128];
        char[] actualChars = new char[128];
        byte[] actualBytes = new byte[128];

        foreach (double value in DoubleValues)
        {
            bool expectedCharSuccess = value.TryFormat(expectedChars, out int expectedCharCount, "R", CultureInfo.InvariantCulture);
            bool actualCharSuccess = ZmijFormatter.TryFormat(value, actualChars, out int actualCharCount, "R", CultureInfo.InvariantCulture);
            bool charsEqual = actualChars.AsSpan(0, actualCharCount).SequenceEqual(expectedChars.AsSpan(0, expectedCharCount));
            bool expectedByteSuccess = value.TryFormat(expectedBytes, out int expectedByteCount, "R", CultureInfo.InvariantCulture);
            bool actualByteSuccess = ZmijFormatter.TryFormatUtf8(value, actualBytes, out int actualByteCount, "R");
            bool bytesEqual = actualBytes.AsSpan(0, actualByteCount).SequenceEqual(expectedBytes.AsSpan(0, expectedByteCount));

            await Assert.That(expectedCharSuccess && actualCharSuccess && expectedByteSuccess && actualByteSuccess).IsTrue();
            await Assert.That(actualCharCount).IsEqualTo(expectedCharCount);
            await Assert.That(charsEqual).IsTrue();
            await Assert.That(actualByteCount).IsEqualTo(expectedByteCount);
            await Assert.That(bytesEqual).IsTrue();
        }
    }

    [Test]
    public async Task FloatSpanFormattingMatchesRuntime()
    {
        char[] expectedChars = new char[128];
        byte[] expectedBytes = new byte[128];
        char[] actualChars = new char[128];
        byte[] actualBytes = new byte[128];

        foreach (float value in FloatValues)
        {
            bool expectedCharSuccess = value.TryFormat(expectedChars, out int expectedCharCount, "R", CultureInfo.InvariantCulture);
            bool actualCharSuccess = ZmijFormatter.TryFormat(value, actualChars, out int actualCharCount, "R", CultureInfo.InvariantCulture);
            bool charsEqual = actualChars.AsSpan(0, actualCharCount).SequenceEqual(expectedChars.AsSpan(0, expectedCharCount));
            bool expectedByteSuccess = value.TryFormat(expectedBytes, out int expectedByteCount, "R", CultureInfo.InvariantCulture);
            bool actualByteSuccess = ZmijFormatter.TryFormatUtf8(value, actualBytes, out int actualByteCount, "R");
            bool bytesEqual = actualBytes.AsSpan(0, actualByteCount).SequenceEqual(expectedBytes.AsSpan(0, expectedByteCount));

            await Assert.That(expectedCharSuccess && actualCharSuccess && expectedByteSuccess && actualByteSuccess).IsTrue();
            await Assert.That(actualCharCount).IsEqualTo(expectedCharCount);
            await Assert.That(charsEqual).IsTrue();
            await Assert.That(actualByteCount).IsEqualTo(expectedByteCount);
            await Assert.That(bytesEqual).IsTrue();
        }
    }

    [Test]
    public async Task TruncatedCharDestinationIsUnchanged()
    {
        char[] buffer = new char[128];
        foreach (double value in DoubleValues)
        {
            string expected = value.ToString("R", CultureInfo.InvariantCulture);
            for (int capacity = 0; capacity < expected.Length; capacity++)
            {
                Array.Fill(buffer, '~');
                bool success = ZmijFormatter.TryFormat(value, buffer.AsSpan(0, capacity), out int written, "R", CultureInfo.InvariantCulture);
                await Assert.That(success).IsFalse();
                await Assert.That(written).IsEqualTo(0);
                await Assert.That(buffer.All(c => c == '~')).IsTrue();
            }
        }
    }

    [Test]
    public async Task TruncatedUtf8DestinationIsUnchanged()
    {
        byte[] buffer = new byte[128];
        foreach (float value in FloatValues)
        {
            string expected = value.ToString("R", CultureInfo.InvariantCulture);
            for (int capacity = 0; capacity < Encoding.UTF8.GetByteCount(expected); capacity++)
            {
                Array.Fill(buffer, (byte)0xA5);
                bool success = ZmijFormatter.TryFormatUtf8(value, buffer.AsSpan(0, capacity), out int written, "R");
                await Assert.That(success).IsFalse();
                await Assert.That(written).IsEqualTo(0);
                await Assert.That(buffer.All(b => b == 0xA5)).IsTrue();
            }
        }
    }

    [Test]
    public async Task ProducerMatchesPublicDecomposition()
    {
        foreach (double value in DoubleValues)
        {
            if (!double.IsFinite(value))
                continue;

            ZmijDecimal expected = ZmijFormatter.ToDecimal(value);
            ProducerResult result = GetProducer(value);
            string expectedDigits = expected.Significand == 0 ? "" : expected.Significand.ToString(CultureInfo.InvariantCulture);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Digits).IsEqualTo(expectedDigits);
            await Assert.That(result.Scale).IsEqualTo(result.DigitCount + expected.Exponent);
            await Assert.That(result.Negative).IsEqualTo(expected.IsNegative);
        }
    }

    [Test]
    public async Task PositivePrecisionMatchesRuntime()
    {
        double[] doubleValues = new[] { 0.0, -0.0, 1.0, 1.25, 1.35, 1.005, 9.995, 99.99, 0.00009999, 1234.5678, double.MaxValue, double.Epsilon };
        float[] floatValues = new[] { 0.0f, -0.0f, 1.0f, 1.25f, 1.35f, 1.005f, 9.995f, 99.99f, 0.00009999f, 1234.5678f, float.MaxValue, float.Epsilon };
        for (int precision = 1; precision <= 17; precision++)
        {
            string doubleFormat = $"G{precision}";
            foreach (double value in doubleValues)
            {
                string expected = value.ToString(doubleFormat, CultureInfo.InvariantCulture);
                await Assert.That(ZmijFormatter.Format(value, doubleFormat, CultureInfo.InvariantCulture)).IsEqualTo(expected);

                char[] chars = new char[128];
                await Assert.That(ZmijFormatter.TryFormat(value, chars, out int charsWritten, doubleFormat, CultureInfo.InvariantCulture)).IsTrue();
                await Assert.That(new string(chars, 0, charsWritten)).IsEqualTo(expected);

                byte[] expectedBytes = new byte[128];
                byte[] actualBytes = new byte[128];
                await Assert.That(value.TryFormat(expectedBytes, out int expectedWritten, doubleFormat, CultureInfo.InvariantCulture)).IsTrue();
                await Assert.That(ZmijFormatter.TryFormatUtf8(value, actualBytes, out int actualWritten, doubleFormat)).IsTrue();
                await Assert.That(actualWritten).IsEqualTo(expectedWritten);
                await Assert.That(actualBytes.AsSpan(0, actualWritten).SequenceEqual(expectedBytes.AsSpan(0, expectedWritten))).IsTrue();
            }

            string floatFormat = $"G{precision}";
            foreach (float value in floatValues)
            {
                string expected = value.ToString(floatFormat, CultureInfo.InvariantCulture);
                await Assert.That(ZmijFormatter.Format(value, floatFormat, CultureInfo.InvariantCulture)).IsEqualTo(expected);
            }
        }

        Random random = new(0x5EED);
        for (int i = 0; i < 1000; i++)
        {
            double value = BitConverter.Int64BitsToDouble(random.NextInt64());
            if (!double.IsFinite(value))
                continue;

            foreach (int precision in new[] { 1, 2, 9, 17 })
            {
                string format = $"G{precision}";
                string expected = value.ToString(format, CultureInfo.InvariantCulture);
                await Assert.That(ZmijFormatter.Format(value, format, CultureInfo.InvariantCulture)).IsEqualTo(expected);
            }
        }
    }

    [Test]
    public async Task UnsupportedFormatsFallBackToRuntime()
    {
        foreach (string format in new[] { "G18", "E3", "F4", "N2", "P1", "C2", "0.000" })
        {
            double doubleValue = 1.23456789;
            float floatValue = 1.2345678f;
            string expectedDouble = doubleValue.ToString(format, CultureInfo.InvariantCulture);
            string expectedFloat = floatValue.ToString(format, CultureInfo.InvariantCulture);
            await Assert.That(ZmijFormatter.Format(doubleValue, format, CultureInfo.InvariantCulture)).IsEqualTo(expectedDouble);
            await Assert.That(ZmijFormatter.Format(floatValue, format, CultureInfo.InvariantCulture)).IsEqualTo(expectedFloat);

            char[] doubleChars = new char[128];
            char[] floatChars = new char[128];
            await Assert.That(ZmijFormatter.TryFormat(doubleValue, doubleChars, out int doubleCharsWritten, format, CultureInfo.InvariantCulture)).IsTrue();
            await Assert.That(ZmijFormatter.TryFormat(floatValue, floatChars, out int floatCharsWritten, format, CultureInfo.InvariantCulture)).IsTrue();
            await Assert.That(new string(doubleChars, 0, doubleCharsWritten)).IsEqualTo(expectedDouble);
            await Assert.That(new string(floatChars, 0, floatCharsWritten)).IsEqualTo(expectedFloat);

            byte[] expectedDoubleBytes = new byte[128];
            byte[] expectedFloatBytes = new byte[128];
            byte[] doubleBytes = new byte[128];
            byte[] floatBytes = new byte[128];
            await Assert.That(doubleValue.TryFormat(expectedDoubleBytes, out int expectedDoubleBytesWritten, format, CultureInfo.InvariantCulture)).IsTrue();
            await Assert.That(floatValue.TryFormat(expectedFloatBytes, out int expectedFloatBytesWritten, format, CultureInfo.InvariantCulture)).IsTrue();
            await Assert.That(ZmijFormatter.TryFormatUtf8(doubleValue, doubleBytes, out int doubleBytesWritten, format)).IsTrue();
            await Assert.That(ZmijFormatter.TryFormatUtf8(floatValue, floatBytes, out int floatBytesWritten, format)).IsTrue();
            await Assert.That(doubleBytesWritten).IsEqualTo(expectedDoubleBytesWritten);
            await Assert.That(floatBytesWritten).IsEqualTo(expectedFloatBytesWritten);
            await Assert.That(doubleBytes.AsSpan(0, doubleBytesWritten).SequenceEqual(expectedDoubleBytes.AsSpan(0, expectedDoubleBytesWritten))).IsTrue();
            await Assert.That(floatBytes.AsSpan(0, floatBytesWritten).SequenceEqual(expectedFloatBytes.AsSpan(0, expectedFloatBytesWritten))).IsTrue();
        }
    }

    [Test]
    public async Task OversizedRPrecisionDefersToRuntime()
    {
        // R precision is ignored, but its numeric value still requires validation:
        // above 999,999,999 the runtime raises FormatException, so the fast path
        // must defer instead of accepting the format. Leading zeros are preserved.
        foreach (string format in new[] { "R999999999", "R0000000007", "R1000000000", "R9999999999", "r1000000000" })
        {
            foreach (double doubleValue in new[] { 1.23456789, double.MaxValue })
            {
                string? expected = null;
                string? expectedFailure = null;
                try
                {
                    expected = doubleValue.ToString(format, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    expectedFailure = ex.GetType().FullName;
                }

                string? actual = null;
                string? actualFailure = null;
                try
                {
                    actual = ZmijFormatter.Format(doubleValue, format, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    actualFailure = ex.GetType().FullName;
                }

                await Assert.That(actualFailure).IsEqualTo(expectedFailure);
                if (expectedFailure is null)
                    await Assert.That(actual).IsEqualTo(expected);
            }

            float floatValue = 1.2345678f;
            string? expectedFloat = null;
            string? expectedFloatFailure = null;
            try
            {
                expectedFloat = floatValue.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                expectedFloatFailure = ex.GetType().FullName;
            }

            string? actualFloat = null;
            string? actualFloatFailure = null;
            try
            {
                actualFloat = ZmijFormatter.Format(floatValue, format, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                actualFloatFailure = ex.GetType().FullName;
            }

            await Assert.That(actualFloatFailure).IsEqualTo(expectedFloatFailure);
            if (expectedFloatFailure is null)
                await Assert.That(actualFloat).IsEqualTo(expectedFloat);

            // Span TryFormat does not normalize invalid specifiers to false: the
            // runtime throws, so compare the full outcome (throw vs false vs text).
            char[] chars = new char[128];
            char[] expectedChars = new char[128];
            await Assert.That(FormatOutcome(() =>
            {
                bool ok = ZmijFormatter.TryFormat(floatValue, chars, out int written, format, CultureInfo.InvariantCulture);
                return (ok, new string(chars, 0, written));
            })).IsEqualTo(FormatOutcome(() =>
            {
                bool ok = floatValue.TryFormat(expectedChars, out int written, format, CultureInfo.InvariantCulture);
                return (ok, new string(expectedChars, 0, written));
            }));

            byte[] bytes = new byte[128];
            byte[] expectedBytes = new byte[128];
            await Assert.That(FormatOutcome(() =>
            {
                bool ok = ZmijFormatter.TryFormatUtf8(floatValue, bytes, out int written, format);
                return (ok, System.Text.Encoding.ASCII.GetString(bytes, 0, written));
            })).IsEqualTo(FormatOutcome(() =>
            {
                bool ok = floatValue.TryFormat(expectedBytes, out int written, format, CultureInfo.InvariantCulture);
                return (ok, System.Text.Encoding.ASCII.GetString(expectedBytes, 0, written));
            }));
        }
    }

    private static string FormatOutcome(Func<(bool Ok, string Text)> format)
    {
        try
        {
            (bool ok, string text) = format();
            return ok ? "OK:" + text : "FAIL";
        }
        catch (Exception ex)
        {
            return "THROW:" + ex.GetType().FullName;
        }
    }

    private static ProducerResult GetProducer(double value)
    {
        Span<byte> digits = stackalloc byte[20];
        bool success = ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative);
        return new ProducerResult(success, Encoding.ASCII.GetString(digits[..digitCount]), digitCount, scale, negative);
    }

    private readonly record struct ProducerResult(bool Success, string Digits, int DigitCount, int Scale, bool Negative);
}
