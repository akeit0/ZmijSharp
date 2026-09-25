using System.Diagnostics;
using System.Globalization;
using System.Text;
using ZmijSharp;

namespace ZmijSharp.Verify;

internal static class ExtendedVerification
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    internal static void RunFormatMatrix()
    {
        NumberFormatInfo custom = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        custom.NumberDecimalSeparator = "::🧪";
        custom.PositiveSign = "POS🧪";
        custom.NegativeSign = "NEG🧪";
        custom.NaNSymbol = "not-a-number🧪";
        custom.PositiveInfinitySymbol = "plus-infinity🧪";
        custom.NegativeInfinitySymbol = "minus-infinity🧪";
        IFormatProvider[] providers = [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("fr-FR"), CultureInfo.GetCultureInfo("sv-SE"), custom];
        string[] matrixFormats = ["", "G", "g", "G0", "g0", "R", "r", "R17", "G1", "G5", "G17", "E2", "F3", "0.000"];
        double[] doubleEdges = [0, -0.0, 1, -1, 0.1, -0.1, Math.PI, double.Epsilon, double.MaxValue, 1e-5, 1e17, double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        float[] floatEdges = [0, -0.0f, 1, -1, 0.1f, -0.1f, MathF.PI, float.Epsilon, float.MaxValue, 1e-5f, 1e9f, float.NaN, float.PositiveInfinity, float.NegativeInfinity];
        ulong state = 0xC017_0A11_5EED_1234UL;
        foreach (IFormatProvider provider in providers)
        {
            foreach (double value in doubleEdges)
                CheckMatrixValue(value, provider, matrixFormats);
            foreach (float value in floatEdges)
                CheckFloatMatrixValue(value, provider, matrixFormats);
            for (int i = 0; i < 2_048; i++)
            {
                CheckMatrixValue(BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref state))), provider, matrixFormats);
                CheckFloatMatrixValue(BitConverter.Int32BitsToSingle(unchecked((int)(uint)NextBits(ref state))), provider, matrixFormats);
            }
            foreach (double value in doubleEdges)
                CheckMatrixDestinations(value, provider);
            foreach (float value in floatEdges)
                CheckFloatMatrixDestinations(value, provider);
        }
        Console.WriteLine("format matrix: four providers, 2,048 random values/type/provider, 14 formats, edge buffer capacities");
    }

    private static void CheckMatrixValue(double value, IFormatProvider provider, string[] matrixFormats)
    {
        Span<char> actual = stackalloc char[512];
        foreach (string format in matrixFormats)
        {
            string expected = value.ToString(format, provider);
            if (!ZmijFormatter.TryFormat(value, actual, out int written, format, provider)
                || !expected.AsSpan().SequenceEqual(actual[..written]))
                Fail("double/format-matrix", value, format, expected, new string(actual[..written]), (ulong)BitConverter.DoubleToInt64Bits(value));
        }
    }

    private static void CheckFloatMatrixValue(float value, IFormatProvider provider, string[] matrixFormats)
    {
        Span<char> actual = stackalloc char[512];
        foreach (string format in matrixFormats)
        {
            string expected = value.ToString(format, provider);
            if (!ZmijFormatter.TryFormat(value, actual, out int written, format, provider)
                || !expected.AsSpan().SequenceEqual(actual[..written]))
                Fail("float/format-matrix", value, format, expected, new string(actual[..written]), (uint)BitConverter.SingleToInt32Bits(value));
        }
    }

    private static void CheckMatrixDestinations(double value, IFormatProvider provider)
    {
        foreach (string format in new[] { "", "G", "R" })
        {
            string expected = value.ToString(format, provider);
            char[] actual = new char[expected.Length];
            for (int capacity = 0; capacity < expected.Length; capacity++)
            {
                Array.Fill(actual, '~');
                if (ZmijFormatter.TryFormat(value, actual.AsSpan(0, capacity), out int written, format, provider)
                    || written != 0 || actual.Any(c => c != '~'))
                    throw new Exception($"double destination modified on failure: {value:R}, {format}, capacity {capacity}");
            }
            if (!ZmijFormatter.TryFormat(value, actual, out int exactWritten, format, provider)
                || !expected.AsSpan().SequenceEqual(actual.AsSpan(0, exactWritten)))
                throw new Exception($"double exact destination mismatch: {value:R}, {format}");
        }
    }

    private static void CheckFloatMatrixDestinations(float value, IFormatProvider provider)
    {
        foreach (string format in new[] { "", "G", "R" })
        {
            string expected = value.ToString(format, provider);
            char[] actual = new char[expected.Length];
            for (int capacity = 0; capacity < expected.Length; capacity++)
            {
                Array.Fill(actual, '~');
                if (ZmijFormatter.TryFormat(value, actual.AsSpan(0, capacity), out int written, format, provider)
                    || written != 0 || actual.Any(c => c != '~'))
                    throw new Exception($"float destination modified on failure: {value:R}, {format}, capacity {capacity}");
            }
            if (!ZmijFormatter.TryFormat(value, actual, out int exactWritten, format, provider)
                || !expected.AsSpan().SequenceEqual(actual.AsSpan(0, exactWritten)))
                throw new Exception($"float exact destination mismatch: {value:R}, {format}");
        }
    }

    internal static void RunDoubleOutputSweep(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        Stopwatch stopwatch = Stopwatch.StartNew();
        ulong state = 0xD0B1E5A7C0DE1234UL;
        for (int i = 0; i < count; i++)
        {
            ulong bits = NextBits(ref state);
            CheckDoubleOutput(BitConverter.Int64BitsToDouble(unchecked((long)bits)), bits);
        }
        stopwatch.Stop();
        Console.WriteLine($"double output sweep: {count:N0} values, seed 0xD0B1E5A7C0DE1234, {stopwatch.Elapsed.TotalSeconds:N3}s");
    }

    internal static void RunUpstreamDifferential(string executable, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        using Process process = new();
        process.StartInfo = new ProcessStartInfo(executable, count.ToString(Invariant))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        process.Start();
        int doubleValues = 0;
        int floatValues = 0;
        int trailingZeroDifferences = 0;
        try
        {
            string? line;
            while ((line = process.StandardOutput.ReadLine()) is not null)
            {
                string[] fields = line.Split(' ');
                if (fields.Length != 5)
                    throw new InvalidOperationException($"Unexpected upstream row: {line}");
                ulong significand = ulong.Parse(fields[2], Invariant);
                int exponent = int.Parse(fields[3], Invariant);
                bool negative = fields[4] == "1";
                if (fields[0] == "D")
                {
                    ulong bits = ulong.Parse(fields[1], NumberStyles.HexNumber, Invariant);
                    ZmijDecimal actual = ZmijFormatter.ToDecimal(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
                    ZmijDecimal upstream = new(significand, exponent, negative);
                    if (actual != upstream)
                    {
                        if (NormalizeDecimal(actual) != NormalizeDecimal(upstream))
                            throw new InvalidOperationException($"Upstream double mismatch at 0x{bits:X16}: upstream={upstream}, local={actual}.");
                        trailingZeroDifferences++;
                    }
                    doubleValues++;
                }
                else if (fields[0] == "F")
                {
                    uint bits = uint.Parse(fields[1], NumberStyles.HexNumber, Invariant);
                    ZmijDecimal actual = ZmijFormatter.ToDecimal(BitConverter.Int32BitsToSingle(unchecked((int)bits)));
                    ZmijDecimal upstream = new(significand, exponent, negative);
                    if (actual != upstream)
                    {
                        if (NormalizeDecimal(actual) != NormalizeDecimal(upstream))
                            throw new InvalidOperationException($"Upstream float mismatch at 0x{bits:X8}: upstream={upstream}, local={actual}.");
                        trailingZeroDifferences++;
                    }
                    floatValues++;
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected upstream type: {fields[0]}");
                }
            }
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Upstream reference exited with code {process.ExitCode}.");
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
        Console.WriteLine($"upstream decomposition differential: {doubleValues:N0} double, {floatValues:N0} float (random count {count:N0}/type plus boundaries); {trailingZeroDifferences:N0} trailing-zero representation differences");
    }

    private static ZmijDecimal NormalizeDecimal(ZmijDecimal decimalValue)
    {
        ulong significand = decimalValue.Significand;
        int exponent = decimalValue.Exponent;
        while (significand != 0 && significand % 10 == 0)
        {
            significand /= 10;
            exponent++;
        }
        return new ZmijDecimal(significand, exponent, decimalValue.IsNegative);
    }

    internal static void CheckFloatOutput(float value, uint bits)
    {
        Span<char> expected = stackalloc char[64];
        Span<char> actual = stackalloc char[64];
        bool expectedSuccess = value.TryFormat(expected, out int expectedWritten, default, Invariant);
        bool actualSuccess = ZmijFormatter.TryFormat(value, actual, out int actualWritten, default, Invariant);
        if (!expectedSuccess || !actualSuccess || !expected[..expectedWritten].SequenceEqual(actual[..actualWritten]))
            Fail("float/output", value, "", new string(expected[..expectedWritten]), new string(actual[..actualWritten]), bits);
    }

    private static void CheckDoubleOutput(double value, ulong bits)
    {
        Span<char> expected = stackalloc char[64];
        Span<char> actual = stackalloc char[64];
        Span<byte> expectedUtf8 = stackalloc byte[64];
        Span<byte> actualUtf8 = stackalloc byte[64];
        bool expectedSuccess = value.TryFormat(expected, out int expectedWritten, default, Invariant);
        bool actualSuccess = ZmijFormatter.TryFormat(value, actual, out int actualWritten, default, Invariant);
        if (!expectedSuccess || !actualSuccess || !expected[..expectedWritten].SequenceEqual(actual[..actualWritten]))
            Fail("double/output", value, "", new string(expected[..expectedWritten]), new string(actual[..actualWritten]), bits);
        bool expectedUtf8Success = value.TryFormat(expectedUtf8, out int expectedBytes, default, Invariant);
        bool actualUtf8Success = ZmijFormatter.TryFormatUtf8(value, actualUtf8, out int actualBytes);
        if (!expectedUtf8Success || !actualUtf8Success || !expectedUtf8[..expectedBytes].SequenceEqual(actualUtf8[..actualBytes]))
            Fail("double/utf8-output", value, "", Encoding.UTF8.GetString(expectedUtf8[..expectedBytes]), Encoding.UTF8.GetString(actualUtf8[..actualBytes]), bits);
    }

    private static ulong NextBits(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static void Fail<T>(string kind, T value, string format, string expected, string actual, ulong bits)
    {
        throw new Exception($"Mismatch {kind}: bits=0x{bits:X16}, value={value}, format={format}, expected='{expected}', actual='{actual}'");
    }
}
