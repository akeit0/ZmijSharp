using System.Globalization;
using ZmijSharp;

namespace UnroundedScaling.Comparison;

// Comparison-only complete formatting path over the unrounded-scaling
// decomposition. Presentation policy (measure, preflight, fixed/scientific
// writes) is ZmijFormatter's, reused verbatim, so the measured
// Zmij-vs-UnroundedScaling delta isolates decomposition cost and formatted
// output is identical by construction (proven by the Verify audit).
//
// Shortest unconstrained path only: counted precision, non-finite values,
// and unsupported formats fall back to the runtime public API — the same
// fallback structure as ZmijFormatter. Zero is handled natively (empty
// digits, scale 0, sign from the bit pattern). The char path widens the
// ASCII digits with a tight loop; the UTF-8 path feeds them direct.
public static class UnroundedScalingFormatter
{
    public static bool TryFormat(double value, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!ZmijFormatter.TryParseSupportedFormat(format, out ZmijFormatter.FormatSpec spec))
            return value.TryFormat(destination, out charsWritten, format, provider);
        if (!double.IsFinite(value))
            return value.TryFormat(destination, out charsWritten, format, provider);

        NumberFormatInfo info = NumberFormatInfo.GetInstance(provider);
        Span<byte> ascii = stackalloc byte[32];
        bool negative = BitConverter.DoubleToInt64Bits(value) < 0;
        int digitCount;
        int scale;
        if (value == 0)
        {
            digitCount = 0;
            scale = 0;
        }
        else
        {
            // Direct TryRun (not the digits wrapper): finite non-zero input is
            // already established, so the wrapper's re-checks would only add
            // noise to the comparison.
            unsafe
            {
                fixed (byte* pointer = ascii)
                {
                    UnroundedBuffer number = new()
                    {
                        DigitsPtr = pointer,
                        Scale = 0,
                        DigitsCount = 0,
                    };
                    if (!UnroundedScaling.TryRun(value, -1, ref number))
                        return value.TryFormat(destination, out charsWritten, format, provider);
                    digitCount = number.DigitsCount;
                    scale = number.Scale;
                }
            }
        }

        Span<char> digits = stackalloc char[20];
        for (int i = 0; i < digitCount; i++)
            digits[i] = (char)ascii[i];
        return ZmijFormatter.TryWriteChar(digits[..digitCount], scale, negative, destination, out charsWritten, spec, info, isFloat: false);
    }

    public static bool TryFormatUtf8(double value, Span<byte> destination, out int bytesWritten,
        ReadOnlySpan<char> format = default)
    {
        if (!ZmijFormatter.TryParseSupportedFormat(format, out ZmijFormatter.FormatSpec spec))
            return value.TryFormat(destination, out bytesWritten, format, CultureInfo.InvariantCulture);
        if (!double.IsFinite(value))
            return value.TryFormat(destination, out bytesWritten, format, CultureInfo.InvariantCulture);

        Span<byte> ascii = stackalloc byte[32];
        bool negative = BitConverter.DoubleToInt64Bits(value) < 0;
        int digitCount;
        int scale;
        if (value == 0)
        {
            digitCount = 0;
            scale = 0;
        }
        else
        {
            // Direct TryRun; see TryFormat for why the digits wrapper is bypassed.
            unsafe
            {
                fixed (byte* pointer = ascii)
                {
                    UnroundedBuffer number = new()
                    {
                        DigitsPtr = pointer,
                        Scale = 0,
                        DigitsCount = 0,
                    };
                    if (!UnroundedScaling.TryRun(value, -1, ref number))
                        return value.TryFormat(destination, out bytesWritten, format, CultureInfo.InvariantCulture);
                    digitCount = number.DigitsCount;
                    scale = number.Scale;
                }
            }
        }

        return ZmijFormatter.TryWriteUtf8(ascii[..digitCount], scale, negative, destination, out bytesWritten, spec, isFloat: false);
    }
}
