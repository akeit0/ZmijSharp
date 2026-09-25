namespace UnroundedScaling.Comparison;

internal unsafe struct UnroundedBuffer
{
    internal byte* DigitsPtr;
    internal int Scale;
    internal int DigitsCount;

    internal void CheckConsistency()
    {
    }
}

internal static class UnroundedFormatting
{
    internal static int CountDigits(ulong value)
    {
        int count = 1;
        while (value >= 10)
        {
            value /= 10;
            count++;
        }
        return count;
    }

    internal static unsafe byte* UInt64ToDecChars(byte* destination, ulong value)
    {
        byte* start = destination;
        byte* current = destination;
        do
        {
            *current++ = (byte)('0' + value % 10);
            value /= 10;
        }
        while (value != 0);

        for (byte* left = start, right = current - 1; left < right; left++, right--)
            (*left, *right) = (*right, *left);
        return start;
    }
}
