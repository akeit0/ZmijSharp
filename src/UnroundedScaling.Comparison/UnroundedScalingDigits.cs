namespace UnroundedScaling.Comparison;

// Comparison adapter over UnroundedScaling.TryRun with the same shape as
// ZmijCore.TryGetSignificantDigits: shortest decimal digits plus scale, no
// presentation. Sign is normalized away (TryRun takes the absolute value),
// so there is no sign output unlike the Zmij side.
public static class UnroundedScalingDigits
{
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
                UnroundedBuffer number = new()
                {
                    DigitsPtr = pointer,
                    Scale = 0,
                    DigitsCount = 0,
                };
                if (!UnroundedScaling.TryRun(value, -1, ref number))
                    return false;

                digitCount = number.DigitsCount;
                scale = number.Scale;
                return true;
            }
        }
    }
}
