extern alias ZmijFull;
extern alias XjbFull;
extern alias XjbCompact;

using BenchmarkDotNet.Attributes;
using ZmijSharp;
using FullCore = ZmijFull::ZmijSharp.ZmijCore;
using XjbDirectCore = XjbFull::ZmijSharp.XjbFloatComparison;
using XjbCompactCore = XjbCompact::ZmijSharp.XjbFloatComparison;

[MemoryDiagnoser]
public class FloatDecompositionBenchmarks
{
    private readonly float[] _values = new float[10_000];

    [Params("Random", "Simple")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        float[] simple = [1.0f, -1.0f, 0.1f, -0.1f, 1.5f, -1.5f,
            10.0f, 100.0f, 12345.6789f, MathF.PI, 1e-15f];
        ulong state = 0x123456789ABCDEF0UL;
        for (int i = 0; i < _values.Length; i++)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            uint bits = (uint)(state * 0x2545F4914F6CDD1DUL);
            float value = Workload == "Simple"
                ? simple[i % simple.Length]
                : BitConverter.Int32BitsToSingle(unchecked((int)bits));
            _values[i] = float.IsFinite(value) && value != 0 ? value : 1.25f;
            var compact = ZmijCore.ToDecimal(_values[i]);
            var full = FullCore.ToDecimal(_values[i]);
            var xjbDirect = XjbDirectCore.ToDecimal(_values[i]);
            var xjbCompact = XjbCompactCore.ToDecimal(_values[i]);
            if (compact.Significand != full.Significand || compact.Exponent != full.Exponent || compact.IsNegative != full.IsNegative
                || compact != xjbDirect || compact != xjbCompact)
                throw new InvalidOperationException($"binary32 producer mismatch at index {i}");
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ZmijCompact()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            ZmijDecimal result = ZmijCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ZmijFull()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            var result = FullCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong XjbDirect()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            ZmijDecimal result = XjbDirectCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong XjbCompact()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            ZmijDecimal result = XjbCompactCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }
}
