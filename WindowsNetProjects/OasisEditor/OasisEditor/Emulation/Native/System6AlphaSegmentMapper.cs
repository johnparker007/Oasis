namespace OasisEditor;

internal static class System6AlphaSegmentMapper
{
    // Native System 6 DLL alpha output is already segment data, not text.
    // DLL author note: bit 0 / 0x1 is top-left outer; bit 1 / 0x2 is top-right outer;
    // numbering continues clockwise around outside first, then inside.
    // The initial Oasis mapping targets the canonical Oasis 14-segment ordering; the runtime
    // adapter expands that canonical mask when a 16-segment visual is selected.
    private static readonly int[] NativeBitToOasisBit =
    [
        0,  // 0x0001: outer top horizontal
        1,  // 0x0002: outer right upper vertical
        2,  // 0x0004: outer right lower vertical
        3,  // 0x0008: outer bottom horizontal
        4,  // 0x0010: outer left lower vertical
        5,  // 0x0020: outer left upper vertical
        6,  // 0x0040: inner left center horizontal
        11, // 0x0080: inner up-left diagonal
        8,  // 0x0100: inner up vertical
        12, // 0x0200: inner up-right diagonal
        7,  // 0x0400: inner right center horizontal
        13, // 0x0800: inner down-right diagonal
        9,  // 0x1000: inner down vertical
        10, // 0x2000: inner down-left diagonal
        14, // 0x4000: optional colon/semicolon segment
        15  // 0x8000: optional colon/semicolon segment
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
