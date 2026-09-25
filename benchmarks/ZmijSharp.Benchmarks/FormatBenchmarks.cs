using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using ZmijSharp;
using UnroundedScaling.Comparison;

[MemoryDiagnoser]
public class FormatBenchmarks
{
    private readonly double[] _values = new double[10_000];
    private readonly float[] _floatValues = new float[10_000];
    private readonly byte[] _bytes = new byte[64];
    private readonly char[] _chars = new char[64];
    private readonly string[] _formats = ["", "G", "R"];

    [GlobalSetup]
    public void Setup()
    {
        ulong state = 0x9E3779B97F4A7C15UL;
        for (int i = 0; i < _values.Length; i++)
        {
            ulong bits = Next(ref state);
            _values[i] = BitConverter.Int64BitsToDouble(unchecked((long)bits));
            _floatValues[i] = BitConverter.Int32BitsToSingle(unchecked((int)(uint)Next(ref state)));
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Utf8", "Double")]
    public void Runtime_Utf8Formatter_Double()
    {
        foreach (double value in _values)
            Utf8Formatter.TryFormat(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Utf8", "Double")]
    public void Zmij_Utf8_Double()
    {
        foreach (double value in _values)
            ZmijFormatter.TryFormatUtf8(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("UnroundedScaling", "Utf8", "Double")]
    public void UnroundedScaling_Utf8_Double()
    {
        foreach (double value in _values)
            UnroundedScalingFormatter.TryFormatUtf8(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("UnroundedScaling", "Double")]
    public int UnroundedScaling_SignificantDigits_Double()
    {
        int total = 0;
        foreach (double value in _values)
        {
            if (UnroundedScalingDigits.TryGetSignificantDigits(value, _bytes, out int digitCount, out _))
                total += digitCount;
        }
        return total;
    }
    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Parse", "Double")]
    public int Zmij_ParseOnly()
    {
        int total = 0;
        for (int i = 0; i < _values.Length; i++)
            if (ZmijFormatter.TryParseSupportedFormat(_formats[i % _formats.Length].AsSpan(), out _))
                total++;
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Char", "Double")]
    public void Runtime_TryFormat_Double()
    {
        foreach (double value in _values)
            value.TryFormat(_chars, out _, default, null);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Char", "Double")]
    public void Zmij_TryFormat_Double()
    {
        foreach (double value in _values)
            ZmijFormatter.TryFormat(value, _chars, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public void Runtime_Counted_G5()
    {
        foreach (double value in _values)
            value.TryFormat(_chars, out _, "G5");
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public void Zmij_Counted_G5()
    {
        foreach (double value in _values)
            ZmijFormatter.TryFormat(value, _chars, out _, "G5");
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("UnroundedScaling", "Char", "Double")]
    public void UnroundedScaling_TryFormat_Double()
    {
        foreach (double value in _values)
            UnroundedScalingFormatter.TryFormat(value, _chars, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Char", "Double")]
    public int Zmij_TryFormat_Truncated()
    {
        // Empty destination: parse + decompose + measure + failed preflight,
        // no bytes written. Directly measures the destination-check path.
        int total = 0;
        foreach (double value in _values)
        {
            ZmijFormatter.TryFormat(value, Span<char>.Empty, out int written);
            total += written;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Char", "Double")]
    public int Runtime_TryFormat_Truncated()
    {
        int total = 0;
        foreach (double value in _values)
        {
            value.TryFormat(Span<char>.Empty, out int written);
            total += written;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Char", "Float")]
    public void Runtime_TryFormat_Float()
    {
        foreach (float value in _floatValues)
            value.TryFormat(_chars, out _, default, null);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Char", "Float")]
    public void Zmij_TryFormat_Float()
    {
        foreach (float value in _floatValues)
            ZmijFormatter.TryFormat(value, _chars, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Utf8", "Float")]
    public void Runtime_Utf8Formatter_Float()
    {
        foreach (float value in _floatValues)
            Utf8Formatter.TryFormat(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Utf8", "Float")]
    public void Zmij_Utf8_Float()
    {
        foreach (float value in _floatValues)
            ZmijFormatter.TryFormatUtf8(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Runtime", "Json", "Double")]
    public int Runtime_JsonWriter_Double()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        foreach (double value in _values)
        {
            if (!double.IsFinite(value))
                continue;
            writer.WriteNumberValue(value);
        }
        writer.WriteEndArray();
        writer.Flush();
        return (int)buffer.WrittenCount;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Zmij", "Json", "Double")]
    public int Zmij_JsonWriter_Double()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(300_000);
        try
        {
            int pos = 0;
            buffer[pos++] = (byte)'[';
            foreach (double value in _values)
            {
                if (!double.IsFinite(value))
                    continue;
                if (ZmijFormatter.TryFormatUtf8(value, buffer.AsSpan(pos), out int written))
                    pos += written;
                buffer[pos++] = (byte)',';
            }
            buffer[pos - 1] = (byte)']';
            return pos;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
