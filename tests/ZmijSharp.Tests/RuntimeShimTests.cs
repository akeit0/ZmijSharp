using System.Buffers;
using System.Buffers.Text;
using System.Text;
using ZmijSharp.RuntimeShim;

namespace ZmijSharp.Tests;

public class RuntimeShimTests
{
    [Test]
    public async Task ShimMatchesInstalledRuntime()
    {
        string[] formats = ["", "G", "g", "R", "r", "G0", "R0"];
        List<double> values =
        [
            0.0, -0.0, 1.0, -1.0, 0.1, 60.0, -60.0, 1.23E+22, Math.PI,
            double.Epsilon, double.MaxValue, double.MinValue,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN,
            1e-4, 1e-5, 1e15, 1e16, 1e21, 1e22,
            BitConverter.Int64BitsToDouble(unchecked((long)0x0010000000000001UL)),
            BitConverter.Int64BitsToDouble(unchecked((long)0x7FEFFFFFFFFFFFFFUL)),
        ];
        ulong state = 0x123456789ABCDEF0UL;
        for (int i = 0; i < 2_000; i++)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            values.Add(BitConverter.Int64BitsToDouble(unchecked((long)(z ^ (z >> 31)))));
        }

        byte[] actual = new byte[64];
        byte[] expected = new byte[64];
        foreach (double value in values)
        {
            foreach (string format in formats)
            {
                string actualOutcome = Outcome(() =>
                {
                    bool ok = Shim.TryFormatDoubleUtf8(value, actual, out int written, format.AsSpan());
                    return (ok, Encoding.ASCII.GetString(actual, 0, written));
                });
                StandardFormat sf = format.Length switch
                {
                    0 => default,
                    1 => new StandardFormat(format[0]),
                    _ => new StandardFormat(format[0], (byte)(format[1] - '0')),
                };
                string expectedOutcome = Outcome(() =>
                {
                    bool ok = Utf8Formatter.TryFormat(value, expected, out int written, sf);
                    return (ok, Encoding.ASCII.GetString(expected, 0, written));
                });
                await Assert.That(actualOutcome).IsEqualTo(expectedOutcome);
            }
        }
    }

    private static string Outcome(Func<(bool Ok, string Text)> format)
    {
        try
        {
            (bool ok, string text) = format();
            return ok ? "OK:" + text : "FAIL";
        }
        catch (Exception ex)
        {
            return "THROW:" + ex.GetType().FullName;
        }
    }
}
