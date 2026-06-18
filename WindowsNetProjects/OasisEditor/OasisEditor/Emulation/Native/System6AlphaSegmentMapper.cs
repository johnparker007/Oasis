namespace OasisEditor;

internal static class System6AlphaSegmentMapper
{
    // Native System 6 DLL alpha output is already segment data, not text.
    // DLL author note: bit 0 / 0x1 is top-left outer; bit 1 / 0x2 is top-right outer;
    // numbering continues clockwise around outside first, then inside.
    // The initial Oasis mapping is intentionally explicit and easy to correct after visual inspection.
    private static readonly int[] NativeBitToOasisBit =
    [
        0,  // 0x0001: top-left outer
        1,  // 0x0002: top-right outer
        2,  // 0x0004: right-upper outer
        3,  // 0x0008: right-lower outer
        4,  // 0x0010: bottom-right outer
        5,  // 0x0020: bottom-left outer
        6,  // 0x0040: left-lower outer
        7,  // 0x0080: left-upper outer
        8,  // 0x0100: inside/crossbar/diagonal segment 0
        9,  // 0x0200: inside/crossbar/diagonal segment 1
        10, // 0x0400: inside/crossbar/diagonal segment 2
        11, // 0x0800: inside/crossbar/diagonal segment 3
        12, // 0x1000: inside/crossbar/diagonal segment 4
        13, // 0x2000: inside/crossbar/diagonal segment 5
        14, // 0x4000: inside/crossbar/diagonal segment 6
        15  // 0x8000: inside/crossbar/diagonal segment 7
    ];

    public static int MapNativeMaskToOasisMask(int nativeMask)
    {
        var mappedMask = 0;
        for (var nativeBit = 0; nativeBit < NativeBitToOasisBit.Length; nativeBit++)
        {
            if ((nativeMask & (1 << nativeBit)) == 0)
            {
                continue;
            }

            mappedMask |= 1 << NativeBitToOasisBit[nativeBit];
        }

        return mappedMask;
    }
}
