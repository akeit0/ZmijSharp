using System.Runtime.CompilerServices;

namespace Shortest.Core;

// The public boundary is identical in both size profiles: finite nonzero
// double -> ASCII digits and decimal scale. The sign is outside this boundary.
public static class Digits
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryGetSignificantDigits(double value, Span<byte> destination, out int digitCount, out int scale)
    {
        digitCount = 0;
        scale = 0;
        if (!double.IsFinite(value) || value == 0 || destination.Length < 32)
            return false;

        return ZmijSharp.ZmijCore.TryGetSignificantDigits(value, destination, out digitCount, out scale, out _);
    }
}
