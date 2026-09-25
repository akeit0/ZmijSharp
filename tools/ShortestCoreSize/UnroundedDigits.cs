using System.Runtime.CompilerServices;
using UnroundedScaling.Comparison;

namespace Shortest.Core;

// Same public boundary and preconditions as the Zmij size profile.
public static class Digits
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryGetSignificantDigits(double value, Span<byte> destination, out int digitCount, out int scale)
    {
        digitCount = 0;
        scale = 0;
        if (!double.IsFinite(value) || value == 0 || destination.Length < 32)
            return false;

        unsafe
        {
            fixed (byte* pointer = destination)
            {
                UnroundedBuffer number = new() { DigitsPtr = pointer };
                if (!global::UnroundedScaling.Comparison.UnroundedScaling.TryRun(value, -1, ref number))
                    return false;

                digitCount = number.DigitsCount;
                scale = number.Scale;
                return true;
            }
        }
    }
}
