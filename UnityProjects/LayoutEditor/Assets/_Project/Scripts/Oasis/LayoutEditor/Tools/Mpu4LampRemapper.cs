using System;
using UnityEngine;

namespace Oasis.LayoutEditor.Tools
{
    public class Mpu4LampRemapper
    {
        /* based on explanation from David Haywood on mame dev forums (github.com)

        Basically each scramble value acts as a 'xor' on each row. I knew what the old layout expected, 
        and was previously being provided, and I knew the difference in the values in my lamp values 
        compared to the old ones.

        From there I created a table of what the 'old' lamp number in the layout was, vs. what it should be.

        I search+replaced each of the lamp values that would need changing, so "lamp64" was renamed "lampxxxx68" 
        for example. After doing all the renames I then removed the 'xxxx' part.

        basically if you want to hand-fix the layouts to work you need to know the (incorrect) CHR lamp table 
        values MFME used and the (correct) CHR lamp table MAME uses.

        expanding upon this...

        If you look at the default debug layout in MAME it is arranged like this

        0 8  16 24 32 40 48 56 64 72 80 88 96  104 112 120
        1 9  17 25 33 41 49 57 65 73 81 89 97  105 113 121
        2 10 18 26 34 42 50 58 66 74 82 90 98  106 114 122
        3 11 19 27 35 43 51 59 67 75 83 91 99  107 115 123
        4 12 20 28 36 44 52 60 68 76 84 92 100 108 116 124
        5 13 21 29 37 45 53 61 69 77 85 93 101 109 117 125
        6 14 22 30 38 46 54 62 70 78 86 94 102 110 118 126
        7 15 23 31 39 47 55 63 71 79 87 95 103 111 119 127
        each of those 8 rows is controlled by a different value in the table

        so in the case of the adders and ladders table
        0x00 affects the lamps on the first row
        0x60 2nd row
        0x40 3rd row
        0x60 4th row
        0x20 5th row
        0x40 6th row
        0x40 7th row
        0x20 8th row

        despite some of the lamp tables having more bits set (I assume these were read from hardware or something)
        the game code only uses 4 bits in each byte for swapping the lamp data

        a value of 0x40 swaps like so (using the first row as an example)


        0  8  16 24  32  40  48  56  64 72 80 88  96  104 112  120 
        to
        64 72 80 88  96  104 112 120 0  8  16 24  32  40   48  56
        which is a XOR of 64 (xor any value with 64) to get new lamp value

        a value of 0x20 swaps like so


        0  8  16 24  32 40 48 56  64 72  80  88   96 104 112 120
        to
        32 40 48 56  0  8  16 24  96 104 112 120  64 72  80  88
        which is a XOR of 32 (xor any value with 32) to get new lamp value

        a value of 0x10 swaps like so


        0  8   16 24 32 40  48 56  64 72  80 88   96  104  112 120
        to
        16 24  0  8  48 56  32 40  80 88  64 72   112 120  96  104
        which is a XOR of 16 (xor any value with 16) to get new lamp value

        and a value of 0x08 swaps like so


        0   8   16  24  32  40  48  56  64  72  80   88   96   104  112  120
        to
        8   0   24  16  40  32  56  48  72  64  88   80   104  96   120  112
        which is a XOR of 8 (xor any value with 8) to get new lamp value

        combinations of bits combine the XOR values, so 0x48 would be 64 + 8, a xor of 72

        as I said, other bits can be ignored, so if 0x04, 0x02, 0x01 or 0x80 are set in the table they 
        appear to have no effect on the lamp scramble (unless they're used for extended lamps, but I'm 
        not seeing that, as I said, I assume in cases where those are set it's because the table was read 
        from hardware, and those bits are set but not used by the lamp scramble)

        so basically,

        to convert from incorrect MFME lamping to correct MAME lamping work you need to use the above 
        information to remove the incorrect MFME scramble from the lamp numbers in the layout, and replace
        it with the correct arrangement, so that when the game writes the descrambled data (as it's the CPU
        reading the table, and doing the descramble) the correct lamps are triggered.

        I did notice some MFME layouts were trying to do all the lamp descrambling in the layout, rather than
        having a correct table for the CPU to use. That approach actually falls apart completely in cases
        where games only go through the lamp scramble function for SOME lamps, and write others directly. 
        (a few do that for clear effects)

        */

        public const int kLampTableSize = 8;

        // doing my rows/colums opposite to MAME, and treating lamp table as affecting 8x columns
        // (16 rows tall base matrix, extended matrix doesn't get scrambled)
        public const int kLampTableColumnCount = 8;
        public const int kLampTableRowCount = 16;


        private byte[] _mfmeLampTable;
        private byte[] _mameLampTable;

        private byte[] _mfmeLampMap;
        private byte[] _mameLampMap;


        public Mpu4LampRemapper(string[] mfmeLampTable, string[] mameLampTable)
        {
            InitialiseLampTables(mfmeLampTable, mameLampTable);
            GenerateLampMaps();
        }

        public byte GetRemappedLampNumber(int mfmeOriginalLampNumber)
        {
            byte trueLampNumber = _mfmeLampMap[mfmeOriginalLampNumber];
            byte lampNumberRequiredToDecodeToTrue = _mameLampMap[trueLampNumber];

            return lampNumberRequiredToDecodeToTrue;
        }

        private void InitialiseLampTables(string[] mfmeLampTable, string[] mameLampTable)
        {
            _mfmeLampTable = GetLampTable(mfmeLampTable);
            _mameLampTable = GetLampTable(mameLampTable);
        }

        private byte[] GetLampTable(string[] lampTableHexStrings)
        {
            byte[] lampTable = new byte[kLampTableSize];
            for (int lampTableIndex = 0; lampTableIndex < kLampTableSize; ++lampTableIndex)
            {
                lampTable[lampTableIndex] = Convert.ToByte(lampTableHexStrings[lampTableIndex], 16);
            }

            return lampTable;
        }

        private void GenerateLampMaps()
        {
            _mfmeLampMap = GetLampMap(_mfmeLampTable);
            _mameLampMap = GetLampMap(_mameLampTable);
        }

        private byte[] GetLampMap(byte[] lampTable)
        {
            byte[] lampMap = new byte[kLampTableColumnCount * kLampTableRowCount];

            byte lampIndex = 0;
            for (int rowIndex = 0; rowIndex < kLampTableRowCount; ++rowIndex)
            {
                for (int columnIndex = 0; columnIndex < kLampTableColumnCount; ++columnIndex)
                {
                    byte chrLampValue = lampTable[columnIndex];

                    // clear bits for 0x01, 0x02, 0x04, 0x80 (0th, 1st, 2nd, 7th) - these are not used in the lamp scramble
                    chrLampValue &= byte.MaxValue ^ (1 << 0);
                    chrLampValue &= byte.MaxValue ^ (1 << 1);
                    chrLampValue &= byte.MaxValue ^ (1 << 2);
                    chrLampValue &= byte.MaxValue ^ (1 << 7);

                    int lampTableIndex = GetLampTableIndex(rowIndex, columnIndex);

                    lampMap[lampTableIndex] = (byte)(lampIndex ^ chrLampValue);

                    ++lampIndex;
                }
            }

            return lampMap;
        }

        private int GetLampTableIndex(int row, int column)
        {
            return (row * kLampTableColumnCount) + column;
        }
    }
}
