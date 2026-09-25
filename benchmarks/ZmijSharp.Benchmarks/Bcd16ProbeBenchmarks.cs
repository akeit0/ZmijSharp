using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using ZmijSharp;

// Scalar BCD8 stage adapted from vitaut/zmij at d1682cb47e67474319ed146d3ca2c0e1a70f9429.
// The original source is MIT licensed; see THIRD-PARTY-NOTICES.txt.
[MemoryDiagnoser]
public class Bcd16ProbeBenchmarks
{
    private readonly ulong[] _values = new ulong[10_000];
    private readonly byte[] _buffer = new byte[32];

    [GlobalSetup]
    public void Setup()
    {
        ulong state = 0x123456789ABCDEF0UL;
        Span<byte> reference = stackalloc byte[32];
        for (int i = 0; i < _values.Length; i++)
        {
            do
            {
                state ^= state >> 12;
                state ^= state << 25;
                state ^= state >> 27;
                double value = BitConverter.Int64BitsToDouble(unchecked((long)(state * 0x2545F4914F6CDD1DUL)));
                _values[i] = double.IsFinite(value) ? ZmijCore.ToDecimal(value).Significand : 0;
            }
            while (_values[i] < 1_000_000_000_000_000UL || _values[i] >= 10_000_000_000_000_000UL);

            PairWrite16(_values[i], reference);
            BcdWrite16(_values[i], _buffer);
            if (!reference[..16].SequenceEqual(_buffer.AsSpan(0, 16)))
                throw new InvalidOperationException($"BCD differs for {_values[i]}.");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 10_000)]
    public int PairTable()
    {
        int sum = 0;
        foreach (ulong value in _values)
        {
            PairWrite16(value, _buffer);
            sum += _buffer[0] + _buffer[15];
        }
        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000)]
    public int OriginalBcd()
    {
        int sum = 0;
        foreach (ulong value in _values)
        {
            BcdWrite16(value, _buffer);
            sum += _buffer[0] + _buffer[15];
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PairWrite16(ulong value, Span<byte> buffer)
    {
        ref byte output = ref MemoryMarshal.GetReference(buffer);
        ref byte pairs = ref MemoryMarshal.GetReference(ZmijCore.DigitPairs);
        int pos = 16;
        while (value >= 100)
        {
            ulong quotient = value / 100;
            int pair = (int)(value - quotient * 100) * 2;
            pos -= 2;
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, pos),
                Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pairs, pair)));
            value = quotient;
        }
        int leadingPair = (int)value * 2;
        Unsafe.WriteUnaligned(ref output,
            Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pairs, leadingPair)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BcdWrite16(ulong value, Span<byte> buffer)
    {
        const ulong hundredMillion = 100_000_000UL;
        ulong high = value / hundredMillion;
        ulong low = value - high * hundredMillion;
        ref byte output = ref MemoryMarshal.GetReference(buffer);
        Unsafe.WriteUnaligned(ref output, ToAsciiBcd8(high));
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, 8), ToAsciiBcd8(low));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToAsciiBcd8(ulong value)
    {
        const ulong div10k = (1UL << 40) / 10_000 + 1;
        const ulong neg10k = (1UL << 32) - 10_000;
        const ulong div100 = (1UL << 19) / 100 + 1;
        const ulong neg100 = (1UL << 16) - 100;
        const ulong div10 = (1UL << 10) / 10 + 1;
        const ulong neg10 = (1UL << 8) - 10;
        unchecked
        {
            ulong a = value + neg10k * ((value * div10k) >> 40);
            ulong b = a + neg100 * (((a * div100) >> 19) & 0x7f0000007fUL);
            ulong c = b + neg10 * (((b * div10) >> 10) & 0xf000f000f000fUL);
            if (BitConverter.IsLittleEndian)
                c = BinaryPrimitives.ReverseEndianness(c);
            return c + 0x3030303030303030UL;
        }
    }
}
