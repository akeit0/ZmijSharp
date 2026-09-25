using BenchmarkDotNet.Attributes;
using ZmijSharp;
using UnroundedScaling.Comparison;

[MemoryDiagnoser]
[BenchmarkCategory("Workload")]
public class WorkloadBenchmarks
{
    private readonly double[] _values = new double[10_000];
    private readonly byte[] _bytes = new byte[64];
    private readonly char[] _chars = new char[64];

    [Params("Random", "Simple", "JsonLike", "LongSignificand", "Extreme")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        double[] simple =
        [
            0.0, -0.0, 1.0, -1.0, 0.1, -0.1, 1.5, -1.5, 10.0, 100.0,
            1000.0, 12345.6789, Math.PI, Math.E, 1e15, 1e-15,
        ];
        double[] extreme =
        [
            double.Epsilon, -double.Epsilon, double.MinValue, double.MaxValue,
            BitConverter.Int64BitsToDouble(unchecked((long)0x0010000000000001UL)),
            BitConverter.Int64BitsToDouble(unchecked((long)0x7FEFFFFFFFFFFFFFUL)),
            1e-300, -1e-300, 1e300, -1e300,
        ];
        double[] jsonLike =
        [
            0.0, 1.0, -1.0, 2.5, 0.5, 10.0, 100.0, 19.99, -45.50, 99.95,
            3.14159, 0.001, 12.345, 123456.789, 1000000.0, 1e9,
        ];
        ulong state = 0x123456789ABCDEF0UL;
        for (int i = 0; i < _values.Length; i++)
        {
            _values[i] = Workload switch
            {
                "Simple" => simple[i % simple.Length],
                "JsonLike" => jsonLike[i % jsonLike.Length],
                "Extreme" => extreme[i % extreme.Length],
                "LongSignificand" => BitConverter.Int64BitsToDouble(unchecked((long)(0x433FFFFFFFFFFFFFUL | (NextBits(ref state) & 0x000FFFFFFFFFFFFFUL)))),
                _ => BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref state))),
            };
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int Zmij_SignificantDigits()
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
    [BenchmarkCategory("Workload", "UnroundedScaling")]
    public int UnroundedScaling_SignificantDigits()
    {
        int total = 0;
        foreach (double value in _values)
        {
            if (UnroundedScalingDigits.TryGetSignificantDigits(value, _bytes, out int digitCount, out _))
                total += digitCount;
        }
        return total;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Workload", "Runtime")]
    public void Runtime_TryFormat()
    {
        foreach (double value in _values)
            value.TryFormat(_chars, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Workload", "Zmij")]
    public void Zmij_TryFormat()
    {
        foreach (double value in _values)
            ZmijFormatter.TryFormat(value, _chars, out _);
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    [BenchmarkCategory("Workload", "UnroundedScaling")]
    public void UnroundedScaling_TryFormat()
    {
        foreach (double value in _values)
            UnroundedScalingFormatter.TryFormat(value, _chars, out _);
    }

    private static ulong NextBits(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
