using System.Runtime.CompilerServices;

namespace ZmijSharp;

/// <summary>Shortest decimal decomposition: value = (-1)^sign * significand * 10^exponent.</summary>
public readonly record struct ZmijDecimal
{
    // Callers use this directly only for canonical results or raw verification tuples.
    internal ZmijDecimal(ulong significand, int exponent, bool isNegative)
    {
        Significand = significand;
        Exponent = exponent;
        IsNegative = isNegative;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ZmijDecimal CreateNormalized(ulong significand, int exponent, bool isNegative)
    {
        if (significand != 0)
        {
            while (significand % 10 == 0)
            {
                significand /= 10;
                exponent++;
            }
        }
        return new ZmijDecimal(significand, exponent, isNegative);
    }

    public ulong Significand { get; }
    public int Exponent { get; }
    public bool IsNegative { get; }
}
