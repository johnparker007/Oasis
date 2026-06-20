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

        // Inner mappings:
        // 8 = center-left
        // 9 = center-right
        // 10 = upper-center
        // 11 = lower-center
        // 12 = lower-left
        // 13 = upper-left
        // 14 = upper-right
        // 15 = lower-right

        10,
        14,
        9,
        15,
        11,
        12,
        8,
        13,
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
