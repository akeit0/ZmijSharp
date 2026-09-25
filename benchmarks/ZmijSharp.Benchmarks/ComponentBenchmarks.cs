using BenchmarkDotNet.Attributes;
using UnroundedScaling.Comparison;
using ZmijSharp;
using UnroundedCore = UnroundedScaling.Comparison.UnroundedScaling;

internal static class ComponentInputs
{
    internal static double[] Create(string workload)
    {
        double[] values = new double[10_000];
        double[] simple = [1.0, -1.0, 0.1, -0.1, 1.5, -1.5, 10.0, 100.0,
            1000.0, 12345.6789, Math.PI, Math.E, 1e15, 1e-15];
        ulong state = 0x123456789ABCDEF0UL;
        for (int i = 0; i < values.Length; i++)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            ulong bits = state * 0x2545F4914F6CDD1DUL;
            double value = workload switch
            {
                "Simple" => simple[i % simple.Length],
                "LongSignificand" => BitConverter.Int64BitsToDouble(unchecked((long)
                    (0x4330000000000000UL | (bits & 0x000FFFFFFFFFFFFFUL)))),
                _ => BitConverter.Int64BitsToDouble(unchecked((long)bits)),
            };
            values[i] = double.IsFinite(value) && value != 0 ? value : 1.25;
        }
        if (workload == "LongSignificand" && values.Distinct().Count() < 9_000)
            throw new InvalidOperationException("LongSignificand corpus must contain varied values.");
        return values;
    }
}

[MemoryDiagnoser]
public class DecompositionBenchmarks
{
    private double[] _values = [];

    [Params("Simple", "LongSignificand", "Random")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        _values = ComponentInputs.Create(Workload);
        foreach (double value in _values)
        {
            ZmijDecimal zmij = ZmijCore.ToDecimal(value);
            (ulong significand, int exponent) = UnroundedCore.GetCanonicalShortest(value);
            if (zmij.Significand != significand || zmij.Exponent != exponent)
                throw new InvalidOperationException($"Canonical decompositions differ for {value:R}.");
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ZmijCanonical()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            ZmijDecimal result = ZmijCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong UnroundedCanonical()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            (ulong significand, int exponent) = UnroundedCore.GetCanonicalShortest(value);
            checksum += significand + (uint)exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong UnroundedRaw()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            (ulong significand, int exponent) = UnroundedCore.GetRawShortest(value);
            checksum += significand + (uint)exponent;
        }
        return checksum;
    }
}

[MemoryDiagnoser]
public class BufferStyleBenchmarks
{
    private readonly ulong[] _digits = new ulong[10_000];
    private readonly int[] _exponents = new int[10_000];
    private readonly byte[] _bytes = new byte[32];

    [Params("Simple", "LongSignificand", "Random")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        double[] values = ComponentInputs.Create(Workload);
        Span<byte> spanOutput = stackalloc byte[32];
        Span<byte> pointerOutput = stackalloc byte[32];
        for (int i = 0; i < values.Length; i++)
        {
            (_digits[i], _exponents[i]) = UnroundedCore.GetRawShortest(values[i]);
            (int count, int scale) = StoreSpan(_digits[i], _exponents[i], spanOutput);
            ZmijDecimal expected = ZmijCore.ToDecimal(values[i]);
            if (count != ZmijCore.CountDigits(expected.Significand)
                || scale != count + expected.Exponent)
                throw new InvalidOperationException($"Buffer output differs for {values[i]:R}.");

            unsafe
            {
                fixed (byte* pointer = pointerOutput)
                {
                    UnroundedBuffer number = new() { DigitsPtr = pointer };
                    UnroundedCore.StoreDigits(ref number, _digits[i], _exponents[i]);
                    if (number.DigitsCount != count || number.Scale != scale
                        || !pointerOutput[..(count + 1)].SequenceEqual(spanOutput[..(count + 1)]))
                        throw new InvalidOperationException($"Buffer variants differ for {values[i]:R}.");
                }
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public unsafe int PointerBuffer()
    {
        int checksum = 0;
        fixed (byte* pointer = _bytes)
        {
            for (int i = 0; i < _digits.Length; i++)
            {
                UnroundedBuffer number = new() { DigitsPtr = pointer };
                UnroundedCore.StoreDigits(ref number, _digits[i], _exponents[i]);
                checksum += number.DigitsCount + number.Scale + pointer[number.DigitsCount - 1];
            }
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int SpanBuffer()
    {
        int checksum = 0;
        for (int i = 0; i < _digits.Length; i++)
        {
            (int count, int scale) = StoreSpan(_digits[i], _exponents[i], _bytes);
            checksum += count + scale + _bytes[count - 1];
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public unsafe int PointerBufferThenWiden()
    {
        int checksum = 0;
        Span<char> chars = stackalloc char[20];
        fixed (byte* pointer = _bytes)
        {
            for (int i = 0; i < _digits.Length; i++)
            {
                UnroundedBuffer number = new() { DigitsPtr = pointer };
                UnroundedCore.StoreDigits(ref number, _digits[i], _exponents[i]);
                for (int j = 0; j < number.DigitsCount; j++)
                    chars[j] = (char)_bytes[j];
                checksum += number.DigitsCount + number.Scale + chars[number.DigitsCount - 1];
            }
        }
        return checksum;
    }

    // Same digit count, byte writer, trailing-zero trim, terminator, and scale
    // as StoreDigits; changes only the pointer-backed result to Span/out values.
    private static (int Count, int Scale) StoreSpan(ulong digits, int exponent, Span<byte> destination)
    {
        int originalCount = UnroundedFormatting.CountDigits(digits);
        int start = UnroundedFormatting.UInt64ToDecChars(destination[..originalCount], originalCount, digits);
        if (start != 0)
            throw new InvalidOperationException("Digit writer did not fill the span.");
        int count = destination[..originalCount].LastIndexOfAnyExcept((byte)'0') + 1;
        destination[count] = 0;
        return (count, originalCount + exponent);
    }
}
