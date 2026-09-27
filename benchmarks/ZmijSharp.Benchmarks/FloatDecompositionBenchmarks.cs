extern alias ZmijFloatFull;

using BenchmarkDotNet.Attributes;
using ZmijSharp;
using HybridCore = ZmijFloatFull::ZmijSharp.ZmijCore;

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
            if (ZmijCore.ToDecimal(_values[i]) != XjbFloatComparison.ToDecimal(_values[i]))
                throw new InvalidOperationException($"xjb32 mismatch at index {i}");
            if (ZmijCore.ToDecimal(_values[i]) != XjbFloatComparison.ToDecimalDirect(_values[i]))
                throw new InvalidOperationException($"xjb32 direct mismatch at index {i}");
            var hybrid = HybridCore.ToDecimal(_values[i]);
            var compact = ZmijCore.ToDecimal(_values[i]);
            if (hybrid.Significand != compact.Significand || hybrid.Exponent != compact.Exponent || hybrid.IsNegative != compact.IsNegative)
                throw new InvalidOperationException($"hybrid Zmij mismatch at index {i}");
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ZmijCanonical()
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
    public ulong XjbCompactCanonical()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            ZmijDecimal result = XjbFloatComparison.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong XjbDirectCanonical()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            ZmijDecimal result = XjbFloatComparison.ToDecimalDirect(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ZmijFloatFullCanonical()
    {
        ulong checksum = 0;
        foreach (float value in _values)
        {
            var result = HybridCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }
}
