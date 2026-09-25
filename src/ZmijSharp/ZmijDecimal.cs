namespace ZmijSharp;

/// <summary>Shortest decimal decomposition: value = (-1)^sign * significand * 10^exponent.</summary>
public readonly record struct ZmijDecimal
{
    // Production decompositions are canonical; the verifier can preserve an
    // upstream raw tuple to identify trailing-zero representation differences.
    internal ZmijDecimal(ulong significand, int exponent, bool isNegative, bool normalize = true)
    {
        if (normalize && significand != 0)
        {
            while (significand % 10 == 0)
            {
                significand /= 10;
                exponent++;
            }
        }

        Significand = significand;
        Exponent = exponent;
        IsNegative = isNegative;
    }

    public ulong Significand { get; }
    public int Exponent { get; }
    public bool IsNegative { get; }
}
