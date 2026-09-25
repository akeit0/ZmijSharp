using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZmijSharp;

/// <summary>
/// Shortest round-trip IEEE-754 formatter whose default/G/R presentation matches .NET.
/// The optimized target is the no-explicit-precision G/R path used by float/double ToString/TryFormat.
/// Unsupported format strings explicitly fall back to the corresponding .NET runtime formatting API.
/// </summary>
public static class ZmijFormatter
{
    private const int DoubleMaxRoundTripDigits = 17;
    private const int FloatMaxRoundTripDigits = 9;

    public static ZmijDecimal ToDecimal(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "ToDecimal requires a finite value.");
        return ZmijCore.ToDecimal(value);
    }

    public static ZmijDecimal ToDecimal(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "ToDecimal requires a finite value.");
        return ZmijCore.ToDecimal(value);
    }

    public static string Format(double value, string? format = null, IFormatProvider? provider = null)
        => FormatCore(value, format, provider, isFloat: false);

    public static string Format(float value, string? format = null, IFormatProvider? provider = null)
        => FormatCore(value, format, provider, isFloat: true);

    public static bool TryFormat(double value, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!TryParseSupportedFormat(format, out FormatSpec spec))
            return value.TryFormat(destination, out charsWritten, format, provider);

        NumberFormatInfo info = NumberFormatInfo.GetInstance(provider);
        return TryFormatChar(value, destination, out charsWritten, spec, info, isFloat: false);
    }

    public static bool TryFormat(float value, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!TryParseSupportedFormat(format, out FormatSpec spec))
            return value.TryFormat(destination, out charsWritten, format, provider);

        NumberFormatInfo info = NumberFormatInfo.GetInstance(provider);
        return TryFormatChar(value, destination, out charsWritten, spec, info, isFloat: true);
    }

    /// <summary>
    /// Formats using the invariant UTF-8 conventions of .NET's UTF-8 numeric formatting path.
    /// </summary>
    public static bool TryFormatUtf8(double value, Span<byte> destination, out int bytesWritten,
        ReadOnlySpan<char> format = default)
    {
        if (!TryParseSupportedFormat(format, out FormatSpec spec))
            return value.TryFormat(destination, out bytesWritten, format, CultureInfo.InvariantCulture);

        return TryFormatUtf8Core(value, destination, out bytesWritten, spec, isFloat: false);
    }

    /// <summary>
    /// Formats using the invariant UTF-8 conventions of .NET's UTF-8 numeric formatting path.
    /// </summary>
    public static bool TryFormatUtf8(float value, Span<byte> destination, out int bytesWritten,
        ReadOnlySpan<char> format = default)
    {
        if (!TryParseSupportedFormat(format, out FormatSpec spec))
            return value.TryFormat(destination, out bytesWritten, format, CultureInfo.InvariantCulture);

        return TryFormatUtf8Core(value, destination, out bytesWritten, spec, isFloat: true);
    }

    private static string FormatCore(double value, string? format, IFormatProvider? provider, bool isFloat)
    {
        if (!TryParseSupportedFormat(format.AsSpan(), out FormatSpec spec))
            return value.ToString(format, provider);

        return FormatSupported(value, spec, provider, isFloat);
    }

    private static string FormatCore(float value, string? format, IFormatProvider? provider, bool isFloat)
    {
        if (!TryParseSupportedFormat(format.AsSpan(), out FormatSpec spec))
            return value.ToString(format, provider);

        return FormatSupported(value, spec, provider, isFloat);
    }

    private static string FormatSupported(double value, FormatSpec spec, IFormatProvider? provider, bool isFloat)
    {
        NumberFormatInfo info = NumberFormatInfo.GetInstance(provider);
        Span<char> initial = stackalloc char[64];
        if (TryFormatChar(value, initial, out int written, spec, info, isFloat))
            return new string(initial[..written]);

        char[]? rented = null;
        try
        {
            int size = 128;
            while (true)
            {
                rented = ArrayPool<char>.Shared.Rent(size);
                if (TryFormatChar(value, rented, out written, spec, info, isFloat))
                    return new string(rented, 0, written);
                ArrayPool<char>.Shared.Return(rented);
                rented = null;
                size = checked(size * 2);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static bool TryFormatChar(double value, Span<char> dst, out int written,
        FormatSpec spec, NumberFormatInfo info, bool isFloat)
    {
        if (!double.IsFinite(value))
        {
            if (double.IsNaN(value))
                return TryCopy(info.NaNSymbol, dst, out written);
            if (double.IsPositiveInfinity(value))
                return TryCopy(info.PositiveInfinitySymbol, dst, out written);
            return TryCopy(info.NegativeInfinitySymbol, dst, out written);
        }

        ZmijDecimal dec = isFloat ? ZmijCore.ToDecimal((float)value) : ZmijCore.ToDecimal(value);
        Span<char> digits = stackalloc char[20];
        int digitCount = dec.Significand == 0 ? 0 : WriteUInt64(dec.Significand, digits);
        int scale = dec.Significand == 0 ? 0 : digitCount + dec.Exponent;
        return TryWriteChar(digits[..digitCount], scale, dec.IsNegative, dst, out written, spec, info, isFloat);
    }

    // Presentation half of TryFormatChar, shared by the comparison-only
    // UnroundedScaling formatter so the measured Zmij-vs-UnroundedScaling
    // delta isolates decomposition cost. Pure code motion: the public path
    // above calls it with identical arguments.
    internal static bool TryWriteChar(ReadOnlySpan<char> digits, int scale, bool negative, Span<char> dst, out int written,
        FormatSpec spec, NumberFormatInfo info, bool isFloat)
    {
        bool scientific = scale > (isFloat ? FloatMaxRoundTripDigits : DoubleMaxRoundTripDigits) || scale < -3;

        int required = Measure(negative, digits.Length, scale, scientific, info);
        if (required > dst.Length)
        {
            written = 0;
            return false;
        }

        int p = 0;
        if (negative)
            Copy(info.NegativeSign, dst, ref p);

        if (scientific)
        {
            dst[p++] = digits[0];
            if (digits.Length > 1)
            {
                Copy(info.NumberDecimalSeparator, dst, ref p);
                CopyDigits(digits, dst, 1, digits.Length - 1, p);
                p += digits.Length - 1;
            }

            dst[p++] = spec.ExponentChar;
            int exponent = scale - 1;
            if (exponent < 0)
            {
                Copy(info.NegativeSign, dst, ref p);
                exponent = -exponent;
            }
            else
            {
                Copy(info.PositiveSign, dst, ref p);
            }
            p += WriteExponent((uint)exponent, dst[p..]);
        }
        else
        {
            p += WriteFixed(digits, scale, info.NumberDecimalSeparator, dst[p..]);
        }

        written = p;
        return true;
    }

    private static bool TryFormatUtf8Core(double value, Span<byte> dst, out int written,
        FormatSpec spec, bool isFloat)
    {
        if (!double.IsFinite(value))
        {
            if (double.IsNaN(value))
                return TryCopy("NaN"u8, dst, out written);
            if (double.IsPositiveInfinity(value))
                return TryCopy("Infinity"u8, dst, out written);
            return TryCopy("-Infinity"u8, dst, out written);
        }

        ZmijDecimal dec = isFloat ? ZmijCore.ToDecimal((float)value) : ZmijCore.ToDecimal(value);
        Span<byte> digits = stackalloc byte[20];
        int digitCount = dec.Significand == 0 ? 0 : WriteUInt64(dec.Significand, digits);
        int scale = dec.Significand == 0 ? 0 : digitCount + dec.Exponent;
        return TryWriteUtf8(digits[..digitCount], scale, dec.IsNegative, dst, out written, spec, isFloat);
    }

    // Presentation half of TryFormatUtf8Core; see TryWriteChar. The byte-digit
    // span feeds WriteFixed directly with no conversion step.
    internal static bool TryWriteUtf8(ReadOnlySpan<byte> digits, int scale, bool negative, Span<byte> dst, out int written,
        FormatSpec spec, bool isFloat)
    {
        bool scientific = scale > (isFloat ? FloatMaxRoundTripDigits : DoubleMaxRoundTripDigits) || scale < -3;

        int required = MeasureInvariant(negative, digits.Length, scale, scientific);
        if (required > dst.Length)
        {
            written = 0;
            return false;
        }

        int p = 0;
        if (negative)
            dst[p++] = (byte)'-';

        if (scientific)
        {
            dst[p++] = digits[0];
            if (digits.Length > 1)
            {
                dst[p++] = (byte)'.';
                CopyDigits(digits, dst, 1, digits.Length - 1, p);
                p += digits.Length - 1;
            }

            dst[p++] = (byte)spec.ExponentChar;
            int exponent = scale - 1;
            if (exponent < 0)
            {
                dst[p++] = (byte)'-';
                exponent = -exponent;
            }
            else
            {
                dst[p++] = (byte)'+';
            }
            p += WriteExponent((uint)exponent, dst[p..]);
        }
        else
        {
            p += WriteFixed(digits, scale, dst[p..]);
        }

        written = p;
        return true;
    }

    private static int Measure(bool negative, int digits, int scale, bool scientific, NumberFormatInfo info)
    {
        int n = negative ? info.NegativeSign.Length : 0;
        if (scientific)
        {
            n += 1;
            if (digits > 1)
                n += info.NumberDecimalSeparator.Length + digits - 1;
            int e = Math.Abs(scale - 1);
            n += 1 + (scale - 1 < 0 ? info.NegativeSign.Length : info.PositiveSign.Length) + Math.Max(2, CountDigits((uint)e));
            return n;
        }

        if (scale > 0)
        {
            n += Math.Max(scale, digits);
            if (digits > scale)
                n += info.NumberDecimalSeparator.Length;
        }
        else
        {
            n += 1;
            if (digits > 0)
                n += info.NumberDecimalSeparator.Length + (-scale) + digits;
        }
        return n;
    }

    private static int MeasureInvariant(bool negative, int digits, int scale, bool scientific)
    {
        int n = negative ? 1 : 0;
        if (scientific)
        {
            n += digits == 1 ? 1 : digits + 1; // leading + optional '.' + rest
            n += 2 + Math.Max(2, CountDigits((uint)Math.Abs(scale - 1))); // E + sign + exponent
            return n;
        }

        if (digits == 0)
            return n + 1;
        if (scale > 0)
            return n + Math.Max(scale, digits) + (digits > scale ? 1 : 0);
        return n + 2 + (-scale) + digits; // 0. + zeroes + digits
    }

    private static unsafe void CopyDigits(ReadOnlySpan<char> source, Span<char> destination, int sourceIndex, int count, int destinationIndex)
    {
        fixed (char* sourcePointer = source)
        fixed (char* destinationPointer = destination)
            Unsafe.CopyBlockUnaligned(destinationPointer + destinationIndex, sourcePointer + sourceIndex, (uint)(count * sizeof(char)));
    }

    private static unsafe void CopyDigits(ReadOnlySpan<byte> source, Span<byte> destination, int sourceIndex, int count, int destinationIndex)
    {
        fixed (byte* sourcePointer = source)
        fixed (byte* destinationPointer = destination)
            Unsafe.CopyBlockUnaligned(destinationPointer + destinationIndex, sourcePointer + sourceIndex, (uint)count);
    }

    private static int WriteFixed(ReadOnlySpan<char> digits, int scale, string separator, Span<char> dst)
        => separator.Length == 1
            ? WriteFixed(digits, scale, separator[0], dst)
            : WriteFixed(digits, scale, separator.AsSpan(), dst);

    private static unsafe int WriteFixed(ReadOnlySpan<char> digits, int scale, char separator, Span<char> dst)
    {
        fixed (char* output = dst)
        fixed (char* source = digits)
        {
            char* writer = output;
            char* start = writer;
            if (scale > 0)
            {
                int integerDigits = Math.Min(scale, digits.Length);
                Unsafe.CopyBlockUnaligned(writer, source, (uint)(integerDigits * sizeof(char)));
                writer += integerDigits;
                int zeroCount = scale - integerDigits;
                for (int i = 0; i < zeroCount; i++)
                    *writer++ = '0';

                if (digits.Length > scale)
                {
                    *writer++ = separator;
                    int trailingDigits = digits.Length - scale;
                    Unsafe.CopyBlockUnaligned(writer, source + scale, (uint)(trailingDigits * sizeof(char)));
                    writer += trailingDigits;
                }
            }
            else
            {
                *writer++ = '0';
                if (digits.Length > 0)
                {
                    *writer++ = separator;
                    for (int i = 0; i < -scale; i++)
                        *writer++ = '0';
                    Unsafe.CopyBlockUnaligned(writer, source, (uint)(digits.Length * sizeof(char)));
                    writer += digits.Length;
                }
            }
            return (int)(writer - start);
        }
    }

    private static int WriteFixed(ReadOnlySpan<char> digits, int scale, ReadOnlySpan<char> separator, Span<char> dst)
    {
        int p = 0;
        if (scale > 0)
        {
            int integerDigits = Math.Min(scale, digits.Length);
            digits[..integerDigits].CopyTo(dst[p..]);
            p += integerDigits;
            for (int i = integerDigits; i < scale; i++)
                dst[p++] = '0';

            if (digits.Length > scale)
            {
                separator.CopyTo(dst[p..]);
                p += separator.Length;
                digits[scale..].CopyTo(dst[p..]);
                p += digits.Length - scale;
            }
        }
        else
        {
            dst[p++] = '0';
            if (!digits.IsEmpty)
            {
                separator.CopyTo(dst[p..]);
                p += separator.Length;
                for (int i = 0; i < -scale; i++)
                    dst[p++] = '0';
                digits.CopyTo(dst[p..]);
                p += digits.Length;
            }
        }
        return p;
    }

    private static unsafe int WriteFixed(ReadOnlySpan<byte> digits, int scale, Span<byte> dst)
    {
        fixed (byte* output = dst)
        fixed (byte* source = digits)
        {
            byte* writer = output;
            byte* start = writer;
            if (scale > 0)
            {
                int integerDigits = Math.Min(scale, digits.Length);
                Unsafe.CopyBlockUnaligned(writer, source, (uint)integerDigits);
                writer += integerDigits;
                int zeroCount = scale - integerDigits;
                Unsafe.InitBlockUnaligned(writer, (byte)'0', (uint)zeroCount);
                writer += zeroCount;

                if (digits.Length > scale)
                {
                    *writer++ = (byte)'.';
                    int trailingDigits = digits.Length - scale;
                    Unsafe.CopyBlockUnaligned(writer, source + scale, (uint)trailingDigits);
                    writer += trailingDigits;
                }
            }
            else
            {
                *writer++ = (byte)'0';
                if (digits.Length > 0)
                {
                    *writer++ = (byte)'.';
                    Unsafe.InitBlockUnaligned(writer, (byte)'0', (uint)-scale);
                    writer -= scale;
                    Unsafe.CopyBlockUnaligned(writer, source, (uint)digits.Length);
                    writer += digits.Length;
                }
            }
            return (int)(writer - start);
        }
    }

    private static unsafe int WriteExponent(uint value, Span<char> dst)
    {
        int digits = Math.Max(2, CountDigits(value));
        fixed (char* output = dst)
        {
            char* writer = output + digits;
            while (writer != output)
            {
                uint q = value / 10;
                *--writer = (char)('0' + value - q * 10);
                value = q;
            }
        }
        return digits;
    }

    private static unsafe int WriteExponent(uint value, Span<byte> dst)
    {
        int digits = Math.Max(2, CountDigits(value));
        fixed (byte* output = dst)
        {
            byte* writer = output + digits;
            while (writer != output)
            {
                uint q = value / 10;
                *--writer = (byte)('0' + value - q * 10);
                value = q;
            }
        }
        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits(uint value)
        => value < 10 ? 1 : value < 100 ? 2 : value < 1_000 ? 3 : 4;

    private static ReadOnlySpan<ulong> DecimalPowers =>
    [
        1UL, 10UL, 100UL, 1_000UL, 10_000UL, 100_000UL, 1_000_000UL, 10_000_000UL,
        100_000_000UL, 1_000_000_000UL, 10_000_000_000UL, 100_000_000_000UL,
        1_000_000_000_000UL, 10_000_000_000_000UL, 100_000_000_000_000UL,
        1_000_000_000_000_000UL, 10_000_000_000_000_000UL, 100_000_000_000_000_000UL,
        1_000_000_000_000_000_000UL, 10_000_000_000_000_000_000UL,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits(ulong value)
    {
        // Callers handle zero separately; Log2 requires nonzero input.
        Debug.Assert(value != 0);
        int digits = ((BitOperations.Log2(value) * 1233) >> 12) + 1;
        if (value >= DecimalPowers[digits])
            return digits + 1;
        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64(ulong value, Span<char> destination)
    {
        int digitCount = CountDigits(value);
        int pos = digitCount;
        while (value >= 100)
        {
            ulong q = value / 100;
            ulong rem = value - q * 100;
            value = q;
            pos -= 2;
            WriteTwoDigits((uint)rem, destination[pos..]);
        }
        if (value >= 10)
        {
            pos -= 2;
            WriteTwoDigits((uint)value, destination[pos..]);
        }
        else
        {
            destination[--pos] = (char)('0' + value);
        }
        return digitCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64(ulong value, Span<byte> destination)
    {
        int digitCount = CountDigits(value);
        int pos = digitCount;
        while (value >= 100)
        {
            ulong q = value / 100;
            ulong rem = value - q * 100;
            value = q;
            pos -= 2;
            WriteTwoDigits((uint)rem, destination[pos..]);
        }
        if (value >= 10)
        {
            pos -= 2;
            WriteTwoDigits((uint)value, destination[pos..]);
        }
        else
        {
            destination[--pos] = (byte)('0' + value);
        }
        return digitCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteTwoDigits(uint value, Span<char> dst)
    {
        int pair = (int)value * 2;
        Debug.Assert(pair + 1 < ZmijCore.DigitPairs.Length);
        ref byte pairBase = ref MemoryMarshal.GetReference(ZmijCore.DigitPairs);
        fixed (char* output = dst)
        {
            output[0] = (char)Unsafe.Add(ref pairBase, pair);
            output[1] = (char)Unsafe.Add(ref pairBase, pair + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteTwoDigits(uint value, Span<byte> dst)
    {
        int pair = (int)value * 2;
        Debug.Assert(pair + 1 < ZmijCore.DigitPairs.Length);
        ref byte pairBase = ref MemoryMarshal.GetReference(ZmijCore.DigitPairs);
        fixed (byte* output = dst)
        {
            Unsafe.WriteUnaligned(output, Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pairBase, pair)));
        }
    }

    // Explicit G precision and non-shortest/custom formats delegate to the runtime.
    internal static bool TryParseSupportedFormat(ReadOnlySpan<char> format, out FormatSpec spec)
    {
        if (format.IsEmpty)
        {
            spec = new('G');
            return true;
        }

        char c = format[0];
        if (c is not ('G' or 'g' or 'R' or 'r'))
        {
            spec = default;
            return false;
        }

        if (c is 'G' or 'g')
        {
            // G0 is the .NET spelling for the shortest round-trip representation.
            if (format.Length == 1 || format.SequenceEqual(c == 'G' ? "G0" : "g0"))
            {
                spec = new(c);
                return true;
            }

            spec = default;
            return false;
        }

        // R precision is accepted but ignored by the fast path; a numeric value above
        // .NET's maximum precision (999,999,999) is deferred to the runtime, which
        // raises FormatException. Any 10+-digit suffix necessarily exceeds the bound.
        if (format.Length > 10)
        {
            spec = default;
            return false;
        }

        int rPrecision = 0;
        for (int i = 1; i < format.Length; i++)
        {
            if ((uint)(format[i] - '0') > 9)
            {
                spec = default;
                return false;
            }

            rPrecision = (rPrecision * 10) + format[i] - '0';
        }

        if (rPrecision > 999_999_999)
        {
            spec = default;
            return false;
        }

        spec = new(c);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Copy(string value, Span<char> dst, ref int p)
    {
        value.AsSpan().CopyTo(dst[p..]);
        p += value.Length;
    }

    private static bool TryCopy(string value, Span<char> dst, out int written)
    {
        if (value.Length > dst.Length)
        {
            written = 0;
            return false;
        }
        value.AsSpan().CopyTo(dst);
        written = value.Length;
        return true;
    }

    private static bool TryCopy(ReadOnlySpan<byte> value, Span<byte> dst, out int written)
    {
        if (value.Length > dst.Length)
        {
            written = 0;
            return false;
        }
        value.CopyTo(dst);
        written = value.Length;
        return true;
    }

    internal readonly struct FormatSpec
    {
        internal FormatSpec(char c)
        {
            ExponentChar = c switch
            {
                'g' or 'r' => 'e',
                _ => 'E',
            };
        }
        internal char ExponentChar { get; }
    }
}
