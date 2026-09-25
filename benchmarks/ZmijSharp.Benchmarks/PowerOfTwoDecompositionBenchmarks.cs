using BenchmarkDotNet.Attributes;
using UnroundedScaling.Comparison;
using ZmijSharp;
using UnroundedCore = UnroundedScaling.Comparison.UnroundedScaling;

[MemoryDiagnoser]
public class PowerOfTwoDecompositionBenchmarks
{
    private readonly double[] _values = new double[10_000];
    private readonly float[] _floatValues = new float[10_000];

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < _values.Length; i++)
        {
            // Every finite normal binary64 exponent, repeated in a fixed corpus.
            int exponent = 1 + i % 2046;
            double value = BitConverter.Int64BitsToDouble((long)((ulong)exponent << 52));
            _values[i] = value;
            ZmijDecimal zmij = ZmijCore.ToDecimal(value);
            (ulong significand, int decimalExponent) = UnroundedCore.GetCanonicalShortest(value);
            if (zmij.Significand != significand || zmij.Exponent != decimalExponent)
                throw new InvalidOperationException($"Power-of-two decomposition differs for exponent {exponent}.");

            int floatExponent = 1 + i % 254;
            _floatValues[i] = BitConverter.Int32BitsToSingle(floatExponent << 23);
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
    public ulong ZmijFloatCanonical()
    {
        ulong checksum = 0;
        foreach (float value in _floatValues)
        {
            ZmijDecimal result = ZmijCore.ToDecimal(value);
            checksum += result.Significand + (uint)result.Exponent;
        }
        return checksum;
    }
}
