using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.GameConfig
{
    /// <summary>
    /// Parses file-level TLV prologue tags from decrypted FML/DAT bytes
    /// (stops at 0xFFFFFFFF; skips 0x43–0x45 string-table continuations).
    /// </summary>
    internal static class FileLevelTagBag
    {
        public const uint TerminationTag = 0xFFFFFFFF;

        public static Dictionary<uint, byte[]> Parse(byte[] plain, uint offset = 0)
        {
            if (plain is null) throw new ArgumentNullException(nameof(plain));
            var result = new Dictionary<uint, byte[]>();
            int pos = checked((int)offset);
            while (pos + 8 <= plain.Length)
            {
                uint tag = BitConverter.ToUInt32(plain, pos);
                uint length = BitConverter.ToUInt32(plain, pos + 4);
                pos += 8;

                if (tag == TerminationTag)
                    break;

                if (length > plain.Length - pos)
                    throw new InvalidDataException(
                        $"TLV tag 0x{tag:X} length 0x{length:X} overruns buffer at 0x{pos:X}");

                var value = new byte[length];
                Buffer.BlockCopy(plain, pos, value, 0, (int)length);
                pos += (int)length;

                if (LengthPrefixedStringTableScanner.IsFileLevelTag(tag))
                {
                    int cont = LengthPrefixedStringTableScanner.MeasureSpan(
                        plain, pos, hostTagKeyByte: (byte)tag);
                    pos += cont;
                }

                result[tag] = value;
            }

            return result;
        }

        /// <summary>
        /// Removes all file-level TLV entries with <paramref name="tagToRemove"/>
        /// (including 0x43–0x45 string-table continuations). Bytes after the
        /// 0xFFFFFFFF terminator are preserved unchanged.
        /// </summary>
        public static byte[] RemoveTag(byte[] plain, uint tagToRemove, out int removedCount)
        {
            if (plain is null) throw new ArgumentNullException(nameof(plain));

            using var ms = new MemoryStream(plain.Length);
            int pos = 0;
            removedCount = 0;

            while (pos + 8 <= plain.Length)
            {
                int headerPos = pos;
                uint tag = BitConverter.ToUInt32(plain, pos);
                uint length = BitConverter.ToUInt32(plain, pos + 4);
                pos += 8;

                if (tag == TerminationTag)
                {
                    ms.Write(plain, headerPos, 8);
                    if (pos < plain.Length)
                        ms.Write(plain, pos, plain.Length - pos);
                    return ms.ToArray();
                }

                if (length > (uint)(plain.Length - pos))
                    throw new InvalidDataException(
                        $"TLV tag 0x{tag:X} length 0x{length:X} overruns buffer at 0x{pos:X}");

                pos += (int)length;
                int cont = 0;
                if (LengthPrefixedStringTableScanner.IsFileLevelTag(tag))
                {
                    cont = LengthPrefixedStringTableScanner.MeasureSpan(
                        plain, pos, hostTagKeyByte: (byte)tag);
                    pos += cont;
                }

                int spanLen = 8 + (int)length + cont;
                if (tag == tagToRemove)
                {
                    removedCount++;
                    continue;
                }

                ms.Write(plain, headerPos, spanLen);
            }

            throw new InvalidDataException("Decrypted FML has no TLV terminator (0xFFFFFFFF).");
        }

        public static string ToHex(byte[] value) => Convert.ToHexString(value);

        public static uint? AsUInt32(byte[] value)
        {
            if (value is null || value.Length < 4) return null;
            return BitConverter.ToUInt32(value, 0);
        }

        public static string AsAsciiNullPadded(byte[] value)
        {
            if (value is null || value.Length == 0) return string.Empty;
            int end = Array.IndexOf(value, (byte)0);
            if (end < 0) end = value.Length;
            return Encoding.ASCII.GetString(value, 0, end);
        }
    }
}
