using System.Diagnostics;
using System.Globalization;
using ZmijSharp;
using UnroundedScaling.Comparison;

if (args.Length == 0)
{
    Environment.SetEnvironmentVariable("DOTNET_TieredCompilation", "0");
    Environment.SetEnvironmentVariable("DOTNET_JitDisasm", "ZmijSharp!* UnroundedScaling.Comparison!*");
    ProcessStartInfo startInfo = new(Environment.ProcessPath!)
    {
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("--emit-disasm");
    using Process process = Process.Start(startInfo)!;
    process.WaitForExit();
    return process.ExitCode;
}

Span<char> chars = stackalloc char[64];
Span<byte> bytes = stackalloc byte[64];
CultureInfo invariant = CultureInfo.InvariantCulture;
double doubleValue = 1.2345678901234567;
float floatValue = 1.2345678f;

ZmijFormatter.TryFormat(doubleValue, chars, out _, "R", invariant);
ZmijFormatter.TryFormat(floatValue, chars, out _, "R", invariant);
ZmijFormatter.TryFormatUtf8(doubleValue, bytes, out _, "R");
ZmijFormatter.TryFormatUtf8(floatValue, bytes, out _, "R");
ZmijCore.TryGetSignificantDigits(doubleValue, bytes, out _, out _, out _);
ZmijCore.TryGetSignificantDigits(floatValue, bytes, out _, out _, out _);
UnroundedScalingDigits.TryGetSignificantDigits(doubleValue, bytes, out _, out _);
return 0;
