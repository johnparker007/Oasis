namespace OasisEditor;

internal static class System6SevenSegmentMapper
{
    // The array index is the Amber/System 6 native bit; the array value is the
    // Oasis canonical bit. Oasis bits 0..7 are A, B, C, D, E, F, G and DP.
    // System 6's seven main segments use the historical reversed bit order;
    // DP remains bit 7. Keep this explicit table easy to correct after live testing.
    private static readonly int[] NativeBitToOasisBit =
    [
        6, // native bit 0 (G)  -> Oasis bit 6 (G)
        5, // native bit 1 (F)  -> Oasis bit 5 (F)
        4, // native bit 2 (E)  -> Oasis bit 4 (E)
        3, // native bit 3 (D)  -> Oasis bit 3 (D)
        2, // native bit 4 (C)  -> Oasis bit 2 (C)
        1, // native bit 5 (B)  -> Oasis bit 1 (B)
        0, // native bit 6 (A)  -> Oasis bit 0 (A)
        7, // native bit 7 (DP) -> Oasis bit 7 (DP)
    ];

    internal static int MapNativeMaskToOasisMask(int nativeMask)
    {
        var oasisMask = 0;
        for (var nativeBit = 0; nativeBit < NativeBitToOasisBit.Length; nativeBit++)
        {
            if ((nativeMask & (1 << nativeBit)) != 0)
                oasisMask |= 1 << NativeBitToOasisBit[nativeBit];
        }

        return oasisMask;
    }
}
