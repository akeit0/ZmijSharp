extern alias ZmijFloatFull;

using BenchmarkDotNet.Attributes;
using ZmijSharp;
using HybridFormatter = ZmijFloatFull::ZmijSharp.ZmijFormatter;

[MemoryDiagnoser]
public class FloatHybridFormatBenchmarks
{
    private readonly float[] _values = new float[10_000];
    private readonly byte[] _utf8 = new byte[64];
    private readonly char[] _utf16 = new char[64];

    [GlobalSetup]
    public void Setup()
    {
        ulong state = 0x1234_5678_9abc_def0UL;
        Span<byte> compact = stackalloc byte[64];
        Span<byte> hybrid = stackalloc byte[64];
        for (int i = 0; i < _values.Length; i++)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            float value = BitConverter.Int32BitsToSingle(unchecked((int)(uint)(state * 0x2545_f491_4f6c_dd1dUL)));
            _values[i] = float.IsFinite(value) && value != 0 ? value : 1.25f;
            if (!ZmijFormatter.TryFormatUtf8(_values[i], compact, out int compactLength)
                || !HybridFormatter.TryFormatUtf8(_values[i], hybrid, out int hybridLength)
                || !compact[..compactLength].SequenceEqual(hybrid[..hybridLength]))
                throw new InvalidOperationException($"Hybrid output mismatch at {i}");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    public int CompactUtf8()
    {
        int total = 0;
        foreach (float value in _values)
        {
            ZmijFormatter.TryFormatUtf8(value, _utf8, out int written);
            total += written;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int HybridUtf8()
    {
        int total = 0;
        foreach (float value in _values)
        {
            HybridFormatter.TryFormatUtf8(value, _utf8, out int written);
            total += written;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int CompactUtf16()
    {
        int total = 0;
        foreach (float value in _values)
        {
            ZmijFormatter.TryFormat(value, _utf16, out int written);
            total += written;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int HybridUtf16()
    {
        int total = 0;
        foreach (float value in _values)
        {
            HybridFormatter.TryFormat(value, _utf16, out int written);
            total += written;
        }
        return total;
    }
}
