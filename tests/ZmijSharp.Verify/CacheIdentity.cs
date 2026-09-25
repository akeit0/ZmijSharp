using System.Numerics;
using ZmijSharp;

namespace ZmijSharp.Verify;

internal static class CacheIdentity
{
    internal static void Check()
    {
        // Exact-arithmetic identity for all 618 compiled cache entries.
        for (int q = -293; q <= 324; q++)
        {
            int g = Pow10BinaryExponent(q);
            BigInteger exact = q >= 0
                ? BigInteger.Pow(10, q) << (127 - g)
                : (BigInteger.One << (127 - g)) / BigInteger.Pow(10, -q);
            ZmijCore.GetPowerOf10(q, out ulong high, out ulong low);
            if (high != (ulong)(exact >> 64) || low != (ulong)(exact & ulong.MaxValue) || (exact >> 128) != 0)
                throw new InvalidOperationException($"Power cache mismatch for q={q}.");
        }
    }

    private static int Pow10BinaryExponent(int q)
    {
        if (q >= 0)
            return (int)BigInteger.Pow(10, q).GetBitLength() - 1;
        return -(int)(BigInteger.Pow(10, -q) - 1).GetBitLength();
    }
}
