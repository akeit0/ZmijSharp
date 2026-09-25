// Adapted byte-specialized helpers from dotnet/runtime at
// https://github.com/dotnet/runtime/commit/56ff851680b3c64a9ecaf543225b9cc948fe0262
// FormattingHelpers.CountDigits.cs, Number.Formatting.cs, and Number.Formatting.Common.cs.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    // Based on do_count_digits from fmt:
    // https://github.com/fmtlib/fmt/blob/662adf4f33346ba9aba8b072194e319869ede54a/include/fmt/format.h#L1124
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CountDigits(ulong value)
    {
        ReadOnlySpan<byte> log2ToPow10 =
        [
            1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  4,  5,  5,  5,
            6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  9,  9,  9,  10, 10, 10,
            10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
            15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
        ];

        nint elementOffset = log2ToPow10[(int)ulong.Log2(value)];
        ReadOnlySpan<ulong> powersOf10 =
        [
            0, 0, 10, 100, 1000, 10000, 100000, 1000000, 10000000,
            100000000, 1000000000, 10000000000, 100000000000,
            1000000000000, 10000000000000, 100000000000000,
            1000000000000000, 10000000000000000, 100000000000000000,
            1000000000000000000, 10000000000000000000
        ];
        ulong powerOf10 = Unsafe.Add(ref MemoryMarshal.GetReference(powersOf10), elementOffset);
        return (int)elementOffset - (value < powerOf10 ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int UInt64ToDecChars(Span<byte> buffer, int index, ulong value)
    {
        if (value >= 10)
        {
            while (value >= 100)
            {
                index -= 2;
                (value, ulong remainder) = Math.DivRem(value, 100);
                WriteTwoDigits((uint)remainder, buffer, index);
            }
            if (value >= 10)
            {
                index -= 2;
                WriteTwoDigits((uint)value, buffer, index);
                return index;
            }
        }
        buffer[--index] = (byte)(value + '0');
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTwoDigits(uint value, Span<byte> buffer, int index)
    {
        Debug.Assert(value <= 99);
        ReadOnlySpan<ushort> twoDigitsBytesTable =
        [
            0x3030, 0x3130, 0x3230, 0x3330, 0x3430, 0x3530, 0x3630, 0x3730, 0x3830, 0x3930,
            0x3031, 0x3131, 0x3231, 0x3331, 0x3431, 0x3531, 0x3631, 0x3731, 0x3831, 0x3931,
            0x3032, 0x3132, 0x3232, 0x3332, 0x3432, 0x3532, 0x3632, 0x3732, 0x3832, 0x3932,
            0x3033, 0x3133, 0x3233, 0x3333, 0x3433, 0x3533, 0x3633, 0x3733, 0x3833, 0x3933,
            0x3034, 0x3134, 0x3234, 0x3334, 0x3434, 0x3534, 0x3634, 0x3734, 0x3834, 0x3934,
            0x3035, 0x3135, 0x3235, 0x3335, 0x3435, 0x3535, 0x3635, 0x3735, 0x3835, 0x3935,
            0x3036, 0x3136, 0x3236, 0x3336, 0x3436, 0x3536, 0x3636, 0x3736, 0x3836, 0x3936,
            0x3037, 0x3137, 0x3237, 0x3337, 0x3437, 0x3537, 0x3637, 0x3737, 0x3837, 0x3937,
            0x3038, 0x3138, 0x3238, 0x3338, 0x3438, 0x3538, 0x3638, 0x3738, 0x3838, 0x3938,
            0x3039, 0x3139, 0x3239, 0x3339, 0x3439, 0x3539, 0x3639, 0x3739, 0x3839, 0x3939,
        ];
        ushort pair = twoDigitsBytesTable[(int)value];
        if (!BitConverter.IsLittleEndian)
            pair = (ushort)((pair << 8) | (pair >> 8));
        MemoryMarshal.Write(buffer.Slice(index, 2), in pair);
    }
}
