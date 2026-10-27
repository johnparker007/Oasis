namespace OasisEditor;

internal static class System6SevenSegmentMapper
{
    // The array index is the Amber/System 6 native bit; the array value is the
    // Oasis canonical bit.
    private static readonly int[] NativeBitToOasisBit =
    [
        7, // native bit 0 (G)  
        6, // native bit 1 (F)  
        5, // native bit 2 (E)  
        4, // native bit 3 (D)  
        3, // native bit 4 (C)  
        2, // native bit 5 (B)  
        1, // native bit 6 (A)  
        0, // native bit 7 (DP) 
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
