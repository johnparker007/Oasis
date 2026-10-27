namespace OasisEditor;

internal static class System6AlphaSegmentMapper
{
    private static readonly int[] NativeBitToOasisBit =
    [
        0,
        1,
        2,
        3,
        4,
        5,
        6,
        7,
        10,
        14,
        9,
        15,
        11,
        12,
        8,
        13,
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
