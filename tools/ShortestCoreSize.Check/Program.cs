using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Shortest.Core;

using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
ulong state = 0xD0B1_E5A7_C0DE_1234UL;
int checkedValues = 0;

Span<byte> digits = stackalloc byte[32];
Span<byte> record = stackalloc byte[32];
for (int i = 0; i < 250_000; i++)
{
    state ^= state >> 12;
    state ^= state << 25;
    state ^= state >> 27;
    ulong bits = state * 0x2545_F491_4F6C_DD1DUL;
    double value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
    if (!double.IsFinite(value) || value == 0)
        continue;

    if (!Digits.TryGetSignificantDigits(value, digits, out int digitCount, out int scale))
        throw new InvalidOperationException($"Producer rejected finite nonzero 0x{bits:X16}.");

    string decimalText = System.Text.Encoding.ASCII.GetString(digits[..digitCount]) + "E" + (scale - digitCount).ToString(CultureInfo.InvariantCulture);
    double parsed = double.Parse(decimalText, CultureInfo.InvariantCulture);
    if (BitConverter.DoubleToInt64Bits(parsed) != BitConverter.DoubleToInt64Bits(Math.Abs(value)))
        throw new InvalidOperationException($"Producer failed round-trip at 0x{bits:X16}: {decimalText}.");

    record[0] = checked((byte)digitCount);
    BinaryPrimitives.WriteInt32LittleEndian(record[1..], scale);
    digits[..digitCount].CopyTo(record[5..]);
    hash.AppendData(record[..(5 + digitCount)]);
    checkedValues++;
}

if (Digits.TryGetSignificantDigits(0, digits, out _, out _)
    || Digits.TryGetSignificantDigits(double.NaN, digits, out _, out _)
    || Digits.TryGetSignificantDigits(1.25, digits[..31], out _, out _))
    throw new InvalidOperationException("Producer accepted an out-of-scope input or destination.");

Console.WriteLine($"{checkedValues} {Convert.ToHexString(hash.GetHashAndReset())}");
