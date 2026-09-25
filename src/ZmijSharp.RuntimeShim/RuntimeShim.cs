// Partial structural port of dotnet/runtime
// src/libraries/System.Private.CoreLib/src/System/Number.Formatting.cs
// TryFormatFloat<double,byte> (commit e8646e7, ~L929) for benchmarking: the same
// call shape (finite-check, format parse, convert, general-writer, copy-if-fits)
// with the Zmij producer in the Grisu3/Dragon4 slot, so benchmarks can split the
// runtime path-shape cost from the conversion cost. Writer/parser adapted from
// src/libraries/Common/src/System/Number.Formatting.Common.cs
// (ParseFormatSpecifier, NumberToString 'G', FormatGeneral, FormatExponent),
// double/byte/invariant shortest scope only.
// Licensed to the .NET Foundation under the MIT license; see THIRD-PARTY-NOTICES.txt.
using System.Diagnostics;

namespace ZmijSharp.RuntimeShim;

public static class Shim
{
    public static bool TryFormatDoubleUtf8(double value, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default)
    {
        if (!double.IsFinite(value))
        {
            ReadOnlySpan<byte> symbol = double.IsNaN(value) ? "NaN"u8 : value < 0.0 ? "-Infinity"u8 : "Infinity"u8;
            if (symbol.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }
            symbol.CopyTo(destination);
            bytesWritten = symbol.Length;
            return true;
        }

        if (!TryParseShortestFormat(format, out char fmt))
        {
            bytesWritten = 0;
            return false;
        }

        Span<byte> digits = stackalloc byte[32];
        if (!ZmijCore.TryGetSignificantDigits(value, digits, out int digitCount, out int scale, out bool negative))
        {
            bytesWritten = 0;
            return false;
        }

        Span<byte> tmp = stackalloc byte[64];
        int len = WriteGeneral(tmp, digits.Slice(0, digitCount), digitCount, scale, negative, fmt == 'G' ? (byte)'E' : (byte)'e');
        if (len > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }
        tmp.Slice(0, len).CopyTo(destination);
        bytesWritten = len;
        return true;
    }

    // ParseFormatSpecifier fast paths, restricted to the shortest double scope.
    public static bool TryParseShortestFormat(ReadOnlySpan<char> format, out char fmt)
    {
        if (format.IsEmpty)
        {
            fmt = 'G';
            return true;
        }
        if (format.Length <= 2 && char.IsAsciiLetter(format[0]) &&
            format[0] is 'G' or 'g' or 'R' or 'r' &&
            (format.Length == 1 || format[1] == '0'))
        {
            // The runtime maps R to G (and r to g): precision ignored, case kept.
            fmt = format[0] is 'R' ? 'G' : format[0] is 'r' ? 'g' : format[0];
            return true;
        }
        fmt = '\0';
        return false;
    }

    // NumberToString 'G' + FormatGeneral for byte/invariant output. Shortest double
    // output never exceeds 17 digits, so the runtime's RoundNumber step is a no-op
    // here. Returns bytes written; the caller guarantees capacity.
    public static int WriteGeneral(Span<byte> destination, ReadOnlySpan<byte> digits, int digitCount, int scale, bool negative, byte expChar)
    {
        Debug.Assert(digitCount <= 17);
        int nMaxDigits = Math.Max(digitCount, 17);
        int pos = 0;
        if (negative)
        {
            destination[pos++] = (byte)'-';
        }

        int digPos = scale;
        bool scientific = false;
        if (digPos > nMaxDigits || digPos < -3)
        {
            digPos = 1;
            scientific = true;
        }

        int intCount = 0;
        if (digPos > 0)
        {
            intCount = Math.Min(digPos, digitCount);
            digits.Slice(0, intCount).CopyTo(destination.Slice(pos));
            pos += intCount;
            for (int i = intCount; i < digPos; i++)
            {
                destination[pos++] = (byte)'0';
            }
        }
        else
        {
            destination[pos++] = (byte)'0';
        }

        ReadOnlySpan<byte> rest = digits.Slice(intCount);
        if (!rest.IsEmpty || digPos < 0)
        {
            destination[pos++] = (byte)'.';
            while (digPos < 0)
            {
                destination[pos++] = (byte)'0';
                digPos++;
            }
            rest.CopyTo(destination.Slice(pos));
            pos += rest.Length;
        }

        if (scientific)
        {
            pos += WriteExponent(destination.Slice(pos), scale - 1, expChar);
        }
        return pos;
    }

    // FormatExponent for byte/invariant output: minimum two digits, always signed.
    public static int WriteExponent(Span<byte> destination, int value, byte expChar)
    {
        int pos = 0;
        destination[pos++] = expChar;
        uint u;
        if (value < 0)
        {
            destination[pos++] = (byte)'-';
            u = (uint)-value;
        }
        else
        {
            destination[pos++] = (byte)'+';
            u = (uint)value;
        }
        int dc = u < 10 ? 1 : u < 100 ? 2 : u < 1000 ? 3 : 4;
        int count = Math.Max(2, dc);
        for (int i = count - 1; i >= 0; i--)
        {
            destination[pos + i] = (byte)('0' + u % 10);
            u /= 10;
        }
        return pos + count;
    }
}
