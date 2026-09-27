using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZmijSharp;

// xjb714/xjb binary32 direct cache, exponent order 44 down to -32.
// Apache-2.0; see THIRD-PARTY-NOTICES.txt.
internal static class XjbFloatDirectCache
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Get(int exponent)
    {
        Debug.Assert(exponent is >= -32 and <= 44);
        return Unsafe.Add(ref MemoryMarshal.GetReference(Powers), 44 - exponent);
    }

    private static ReadOnlySpan<ulong> Powers =>
    [
        0x8f7e32ce7bea5c70UL, 0xe596b7b0c643c71aUL, 0xb7abc627050305aeUL, 0x92efd1b8d0cf37bfUL,
        0xeb194f8e1ae525feUL, 0xbc143fa4e250eb32UL, 0x96769950b50d88f5UL, 0xf0bdc21abb48db21UL,
        0xc097ce7bc90715b4UL, 0x9a130b963a6c115dUL, 0xf684df56c3e01bc7UL, 0xc5371912364ce306UL,
        0x9dc5ada82b70b59eUL, 0xfc6f7c4045812297UL, 0xc9f2c9cd04674edfUL, 0xa18f07d736b90be6UL,
        0x813f3978f8940985UL, 0xcecb8f27f4200f3aUL, 0xa56fa5b99019a5c8UL, 0x84595161401484a0UL,
        0xd3c21bcecceda100UL, 0xa968163f0a57b400UL, 0x878678326eac9000UL, 0xd8d726b7177a8000UL,
        0xad78ebc5ac620000UL, 0x8ac7230489e80000UL, 0xde0b6b3a76400000UL, 0xb1a2bc2ec5000000UL,
        0x8e1bc9bf04000000UL, 0xe35fa931a0000000UL, 0xb5e620f480000000UL, 0x9184e72a00000000UL,
        0xe8d4a51000000000UL, 0xba43b74000000000UL, 0x9502f90000000000UL, 0xee6b280000000000UL,
        0xbebc200000000000UL, 0x9896800000000000UL, 0xf424000000000000UL, 0xc350000000000000UL,
        0x9c40000000000000UL, 0xfa00000000000000UL, 0xc800000000000000UL, 0xa000000000000000UL,
        0x8000000000000000UL, 0xcccccccccccccccdUL, 0xa3d70a3d70a3d70bUL, 0x83126e978d4fdf3cUL,
        0xd1b71758e219652cUL, 0xa7c5ac471b478424UL, 0x8637bd05af6c69b6UL, 0xd6bf94d5e57a42bdUL,
        0xabcc77118461cefdUL, 0x89705f4136b4a598UL, 0xdbe6fecebdedd5bfUL, 0xafebff0bcb24aaffUL,
        0x8cbccc096f5088ccUL, 0xe12e13424bb40e14UL, 0xb424dc35095cd810UL, 0x901d7cf73ab0acdaUL,
        0xe69594bec44de15cUL, 0xb877aa3236a4b44aUL, 0x9392ee8e921d5d08UL, 0xec1e4a7db69561a6UL,
        0xbce5086492111aebUL, 0x971da05074da7befUL, 0xf1c90080baf72cb2UL, 0xc16d9a0095928a28UL,
        0x9abe14cd44753b53UL, 0xf79687aed3eec552UL, 0xc612062576589ddbUL, 0x9e74d1b791e07e49UL,
        0xfd87b5f28300ca0eUL, 0xcad2f7f5359a3b3fUL, 0xa2425ff75e14fc32UL, 0x81ceb32c4b43fcf5UL,
        0xcfb11ead453994bbUL,
    ];
}
