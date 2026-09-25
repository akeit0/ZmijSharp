using System.Buffers.Text;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using ZmijSharp;
using ZmijSharp.RuntimeShim;

[MemoryDiagnoser]
[BenchmarkCategory("Shim", "Utf8", "Double")]
public class PathSplitBenchmarks
{
    private readonly double[] _values = new double[10_000];
    private readonly byte[] _bytes = new byte[64];
    private readonly string[] _formats = ["", "G", "R"];
    private byte[][] _digits = [];
    private int[] _scales = [];
    private bool[] _negatives = [];

    [GlobalSetup]
    public void Setup()
    {
        ulong state = 0x9E3779B97F4A7C15UL;
        _digits = new byte[_values.Length][];
        _scales = new int[_values.Length];
        _negatives = new bool[_values.Length];
        for (int i = 0; i < _values.Length; i++)
        {
            ulong bits = Next(ref state);
            double value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
            _values[i] = value;
            if (double.IsFinite(value))
            {
                ZmijDecimal d = ZmijFormatter.ToDecimal(value);
                string s = d.Significand == 0 ? "" : d.Significand.ToString(CultureInfo.InvariantCulture);
                _digits[i] = Encoding.ASCII.GetBytes(s);
                _scales[i] = d.Significand == 0 ? 0 : s.Length + d.Exponent;
                _negatives[i] = d.IsNegative;
            }
            else
            {
                _digits[i] = [];
                _scales[i] = 0;
                _negatives[i] = false;
            }
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    public void Runtime()
    {
        foreach (double value in _values)
            Utf8Formatter.TryFormat(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public void Zmij()
    {
        foreach (double value in _values)
            ZmijFormatter.TryFormatUtf8(value, _bytes, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int SignificantDigits()
    {
        int total = 0;
        foreach (double value in _values)
        {
            ZmijCore.TryGetSignificantDigits(value, _bytes, out int digitCount, out _, out _);
            total += digitCount;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int Shim_Full()
    {
        int total = 0;
        foreach (double value in _values)
            if (Shim.TryFormatDoubleUtf8(value, _bytes, out int written))
                total += written;
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int Shim_ParseOnly()
    {
        int total = 0;
        for (int i = 0; i < _values.Length; i++)
            if (Shim.TryParseShortestFormat(_formats[i % _formats.Length].AsSpan(), out _))
                total++;
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int Shim_WriteOnly()
    {
        int total = 0;
        for (int i = 0; i < _values.Length; i++)
        {
            if (!double.IsFinite(_values[i]))
                continue;
            total += Shim.WriteGeneral(_bytes, _digits[i], _digits[i].Length, _scales[i], _negatives[i], (byte)'E');
        }
        return total;
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
