extern alias ZmijFull;

using BenchmarkDotNet.Attributes;
using ZmijSharp;
using FullCore = ZmijFull::ZmijSharp.ZmijCore;
using UnroundedCore = UnroundedScaling.Comparison.UnroundedScaling;

// The full cache is source-linked into a separate assembly so all three
// producers can be measured over the same corpus in one BenchmarkDotNet run.
[MemoryDiagnoser]
public class CacheProfileDecompositionBenchmarks
{
    private double[] _values = [];

    [Params("Simple", "LongSignificand", "Random")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        if (typeof(ZmijCore).Assembly.GetType("ZmijSharp.CompactPow10Cache") is null
            || typeof(FullCore).Assembly.GetType("ZmijSharp.Pow10Tables") is null)
            throw new InvalidOperationException("Expected separate compact and full cache assemblies.");

        _values = ComponentInputs.Create(Workload);
        foreach (double value in _values)
        {
            ZmijDecimal compact = ZmijCore.ToDecimal(value);
            var full = FullCore.ToDecimal(value);
            (ulong significand, int exponent) = UnroundedCore.GetCanonicalShortest(value);
            if (compact.Significand != significand || compact.Exponent != exponent
                || full.Significand != significand || full.Exponent != exponent
                || compact.IsNegative != full.IsNegative)
                throw new InvalidOperationException($"Canonical decompositions differ for {value:R}.");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    public ulong ZmijCompact()
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
    public ulong ZmijFull()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            var result = FullCore.ToDecimal(value);
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
}
