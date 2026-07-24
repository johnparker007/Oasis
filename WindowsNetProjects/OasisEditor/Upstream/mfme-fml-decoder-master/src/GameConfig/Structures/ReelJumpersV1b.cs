using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.GameConfig.Structures
{
    /// <summary>
    /// MPU5 Reel Jumpers V1b dual-ternary encoding
    /// (mode tag 0x4F; RJ1 A/B = 0x50/0x27; RJ2 A/B = 0x51/0x50).
    /// </summary>
    internal static class ReelJumpersV1b
    {
        public const uint ModeTag = 0x4F;
        public const uint Rj1SideA = 0x50;
        public const uint Rj1SideB = 0x27;
        public const uint Rj2SideA = 0x51;
        public const uint Rj2SideB = 0x50;

        // Slot 1..5 → bits 3..7
        private static readonly int[] SlotBits = { 3, 4, 5, 6, 7 };

        public static string DecodeModeLabel(uint mode, int bank)
        {
            if (bank == 1)
            {
                if ((mode & 5u) == 5u) return "V1b";
                if ((mode & 1u) == 1u) return "V1a";
                return "Old1";
            }

            if ((mode & 10u) == 10u) return "V1b";
            if ((mode & 2u) == 2u) return "V1a";
            return "Old";
        }

        public static bool IsV1b(uint mode, int bank) =>
            bank == 1 ? (mode & 5u) == 5u : (mode & 10u) == 10u;

        public static string[] DecodeSlots(byte[] tagA, byte[] tagB)
        {
            var slots = new string[5];
            for (int i = 0; i < 5; i++)
            {
                string a = DecodeSide(tagA, SlotBits[i]);
                string b = DecodeSide(tagB, SlotBits[i]);
                slots[i] = a + " " + b;
            }

            return slots;
        }

        public static string DecodeSide(byte[] tag, int bit)
        {
            byte b0 = tag != null && tag.Length > 0 ? tag[0] : (byte)0;
            byte b4 = tag != null && tag.Length > 4 ? tag[4] : (byte)0;
            bool lo = (b0 & (1 << bit)) != 0;
            bool hi = (b4 & (1 << bit)) != 0;
            if (lo && hi) return "??";
            if (hi) return "hi";
            if (lo) return "lo";
            return "out";
        }

        public static Dictionary<string, bool> DecodeOldCheckboxes(byte[] tag)
        {
            // J1 byte0 bit3, J2 byte4 bit3, J3 byte0 bit4, J4 byte4 bit4,
            // J5 byte0 bit6, J6 byte4 bit6, J7 byte0 bit7, J8 byte4 bit7
            (string Name, int Byte, int Bit)[] map =
            {
                ("J1", 0, 3), ("J2", 4, 3),
                ("J3", 0, 4), ("J4", 4, 4),
                ("J5", 0, 6), ("J6", 4, 6),
                ("J7", 0, 7), ("J8", 4, 7),
            };

            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (name, byteIndex, bit) in map)
            {
                byte v = tag != null && byteIndex < tag.Length ? tag[byteIndex] : (byte)0;
                checks[name] = (v & (1 << bit)) != 0;
            }

            return checks;
        }
    }
}
