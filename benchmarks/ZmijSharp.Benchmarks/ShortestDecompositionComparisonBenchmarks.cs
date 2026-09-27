extern alias ZmijFull;
extern alias ZmijPreShortcut;
extern alias XjbFull;
extern alias XjbCompact;

using BenchmarkDotNet.Attributes;
using ZmijSharp;
using FullCore = ZmijFull::ZmijSharp.ZmijCore;
using PreShortcutCore = ZmijPreShortcut::ZmijSharp.ZmijCore;
using XjbFullCore = XjbFull::ZmijSharp.XjbDoubleComparison;
using XjbCompactCore = XjbCompact::ZmijSharp.XjbDoubleComparison;
using UnroundedCore = UnroundedScaling.Comparison.UnroundedScaling;

// The full cache is source-linked into a separate assembly so all three
// producers can be measured over the same corpus in one BenchmarkDotNet run.
[MemoryDiagnoser]
public class ShortestDecompositionComparisonBenchmarks
{
    private double[] _values = [];

    [Params("Simple", "LongSignificand", "Random", "Integer")]
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
            var before = PreShortcutCore.ToDecimal(value);
            ZmijDecimal xjb = XjbFullCore.ToDecimal(value);
            ZmijDecimal xjbCompact = XjbCompactCore.ToDecimal(value);
            (ulong significand, int exponent) = UnroundedCore.GetCanonicalShortest(value);
            if (compact.Significand != significand || compact.Exponent != exponent
                || full.Significand != significand || full.Exponent != exponent
                || compact.IsNegative != full.IsNegative
                || before.Significand != significand || before.Exponent != exponent
                || before.IsNegative != compact.IsNegative
                || xjb.Significand != significand || xjb.Exponent != exponent
                || xjb.IsNegative != compact.IsNegative
                || xjbCompact.Significand != significand || xjbCompact.Exponent != exponent
                || xjbCompact.IsNegative != compact.IsNegative)
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
    public ulong ZmijPreShortcut()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            var result = PreShortcutCore.ToDecimal(value);
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

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong XjbFullCacheCanonical()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            ZmijDecimal result = XjbFullCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong XjbCompactCacheCanonical()
    {
        ulong checksum = 0;
        foreach (double value in _values)
        {
            ZmijDecimal result = XjbCompactCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }
}
