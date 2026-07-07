using System;
using System.Numerics;

namespace MfmeFmlDecoder.Decryption
{
    internal static class Ripemd128
    {
        public static byte[] ComputeHash(byte[] data)
        {
            long ml = (long)data.Length * 8;
            int padLen = (56 - (data.Length + 1) % 64 + 64) % 64;
            int totalLen = data.Length + 1 + padLen + 8;
            byte[] padded = new byte[totalLen];
            Array.Copy(data, padded, data.Length);
            padded[data.Length] = 0x80;
            BitConverter.TryWriteBytes(new Span<byte>(padded, totalLen - 8, 8), ml);

            uint h0 = IV[0], h1 = IV[1], h2 = IV[2], h3 = IV[3];
            Span<uint> X = stackalloc uint[16];

            for (int i = 0; i < totalLen; i += 64)
            {
                Span<byte> block = new Span<byte>(padded, i, 64);
                for (int k = 0; k < 16; k++)
                    X[k] = BitConverter.ToUInt32(block.Slice(k * 4, 4));

                uint al = h0, bl = h1, cl = h2, dl = h3;
                uint ar = h0, br = h1, cr = h2, dr = h3;

                for (int j = 0; j < 16; j++)
                {
                    uint t = al + F1(bl, cl, dl) + X[WL[j]] + KL[0];
                    t = BitOperations.RotateLeft(t, SL[j]);
                    al = dl; dl = cl; cl = bl; bl = t;
                }
                for (int j = 16; j < 32; j++)
                {
                    uint t = al + F2(bl, cl, dl) + X[WL[j]] + KL[1];
                    t = BitOperations.RotateLeft(t, SL[j]);
                    al = dl; dl = cl; cl = bl; bl = t;
                }
                for (int j = 32; j < 48; j++)
                {
                    uint t = al + F3(bl, cl, dl) + X[WL[j]] + KL[2];
                    t = BitOperations.RotateLeft(t, SL[j]);
                    al = dl; dl = cl; cl = bl; bl = t;
                }
                for (int j = 48; j < 64; j++)
                {
                    uint t = al + F4(bl, cl, dl) + X[WL[j]] + KL[3];
                    t = BitOperations.RotateLeft(t, SL[j]);
                    al = dl; dl = cl; cl = bl; bl = t;
                }

                for (int j = 0; j < 16; j++)
                {
                    uint t = ar + F4(br, cr, dr) + X[WR[j]] + KR[0];
                    t = BitOperations.RotateLeft(t, SR[j]);
                    ar = dr; dr = cr; cr = br; br = t;
                }
                for (int j = 16; j < 32; j++)
                {
                    uint t = ar + F3(br, cr, dr) + X[WR[j]] + KR[1];
                    t = BitOperations.RotateLeft(t, SR[j]);
                    ar = dr; dr = cr; cr = br; br = t;
                }
                for (int j = 32; j < 48; j++)
                {
                    uint t = ar + F2(br, cr, dr) + X[WR[j]] + KR[2];
                    t = BitOperations.RotateLeft(t, SR[j]);
                    ar = dr; dr = cr; cr = br; br = t;
                }
                for (int j = 48; j < 64; j++)
                {
                    uint t = ar + F1(br, cr, dr) + X[WR[j]] + KR[3];
                    t = BitOperations.RotateLeft(t, SR[j]);
                    ar = dr; dr = cr; cr = br; br = t;
                }

                uint tmp = h1 + cl + dr;
                h1 = h2 + dl + ar;
                h2 = h3 + al + br;
                h3 = h0 + bl + cr;
                h0 = tmp;
            }

            byte[] result = new byte[16];
            BitConverter.TryWriteBytes(new Span<byte>(result, 0, 4), h0);
            BitConverter.TryWriteBytes(new Span<byte>(result, 4, 4), h1);
            BitConverter.TryWriteBytes(new Span<byte>(result, 8, 4), h2);
            BitConverter.TryWriteBytes(new Span<byte>(result, 12, 4), h3);
            return result;
        }

        private static uint F1(uint x, uint y, uint z) => x ^ y ^ z;
        private static uint F2(uint x, uint y, uint z) => (x & y) | (~x & z);
        private static uint F3(uint x, uint y, uint z) => (x | ~y) ^ z;
        private static uint F4(uint x, uint y, uint z) => (x & z) | (y & ~z);

        private static readonly uint[] IV = { 0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476 };
        private static readonly uint[] KL = { 0x00000000, 0x5A827999, 0x6ED9EBA1, 0x8F1BBCDC };
        private static readonly uint[] KR = { 0x50A28BE6, 0x5C4DD124, 0x6D703EF3, 0x00000000 };

        private static readonly int[] WL = {
             0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15,
             7,  4, 13,  1, 10,  6, 15,  3, 12,  0,  9,  5,  2, 14, 11,  8,
             3, 10, 14,  4,  9, 15,  8,  1,  2,  7,  0,  6, 13, 11,  5, 12,
             1,  9, 11, 10,  0,  8, 12,  4, 13,  3,  7, 15, 14,  5,  6,  2,
        };
        private static readonly int[] WR = {
             5, 14,  7,  0,  9,  2, 11,  4, 13,  6, 15,  8,  1, 10,  3, 12,
             6, 11,  3,  7,  0, 13,  5, 10, 14, 15,  8, 12,  4,  9,  1,  2,
            15,  5,  1,  3,  7, 14,  6,  9, 11,  8, 12,  2, 10,  0,  4, 13,
             8,  6,  4,  1,  3, 11, 15,  0,  5, 12,  2, 13,  9,  7, 10, 14,
        };
        private static readonly int[] SL = {
            11, 14, 15, 12,  5,  8,  7,  9, 11, 13, 14, 15,  6,  7,  9,  8,
             7,  6,  8, 13, 11,  9,  7, 15,  7, 12, 15,  9, 11,  7, 13, 12,
            11, 13,  6,  7, 14,  9, 13, 15, 14,  8, 13,  6,  5, 12,  7,  5,
            11, 12, 14, 15, 14, 15,  9,  8,  9, 14,  5,  6,  8,  6,  5, 12,
        };
        private static readonly int[] SR = {
             8,  9,  9, 11, 13, 15, 15,  5,  7,  7,  8, 11, 14, 14, 12,  6,
             9, 13, 15,  7, 12,  8,  9, 11,  7,  7, 12,  7,  6, 15, 13, 11,
             9,  7, 15, 11,  8,  6,  6, 14, 12, 13,  5, 14, 13, 13,  7,  5,
            15,  5,  8, 11, 14, 14,  6, 14,  6,  9, 12,  9, 12,  5, 15,  8,
        };
    }
}
