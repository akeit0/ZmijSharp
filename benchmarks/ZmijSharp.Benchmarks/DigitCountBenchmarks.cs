using BenchmarkDotNet.Attributes;
using ZmijSharp;
using UnroundedScaling.Comparison;

[MemoryDiagnoser]
public class DigitCountBenchmarks
{
    private readonly ulong[] _values = new ulong[10_000];

    [Params("Simple", "LongSignificand", "Random")]
    public string Workload { get; set; } = "Random";

    [GlobalSetup]
    public void Setup()
    {
        double[] simple = [0.0, 1.0, 0.1, 1.5, 10.0, 100.0, 12345.6789, Math.PI];
        ulong state = 0x123456789ABCDEF0UL;
        for (int i = 0; i < _values.Length; i++)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            ulong bits = state * 0x2545F4914F6CDD1DUL;
            double value = Workload switch
            {
                "Simple" => simple[i % simple.Length],
                "LongSignificand" => BitConverter.Int64BitsToDouble(unchecked((long)(0x433FFFFFFFFFFFFFUL | (bits & 0x000FFFFFFFFFFFFFUL)))),
                _ => BitConverter.Int64BitsToDouble(unchecked((long)bits)),
            };
            _values[i] = double.IsFinite(value) ? ZmijCore.ToDecimal(value).Significand : 1;
            if (_values[i] == 0)
                _values[i] = 1;
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    public int Zmij()
    {
        int sum = 0;
        foreach (ulong value in _values)
            sum += ZmijCore.CountDigits(value);
        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int CoreLibPort()
    {
        int sum = 0;
        foreach (ulong value in _values)
            sum += UnroundedFormatting.CountDigits(value);
        return sum;
    }
}
