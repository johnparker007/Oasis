using System;
using System.Buffers.Binary;
using System.IO;

namespace MfmeFmlDecoder.Decoder
{
    /// <summary>
    /// Scans MFME length-prefixed, zero-terminated ASCII string tables used on file-level tags
    /// <c>0x43</c>–<c>0x45</c> and nested extended tags with the same record layout.
    /// </summary>
    internal static class LengthPrefixedStringTableScanner
    {
        internal static bool IsFileLevelTag(uint tag) => tag is 0x43 or 0x44 or 0x45;

        internal static int MeasureSpan(byte[] data, int offset, byte hostTagKeyByte, int scanEndExclusive = -1)
        {
            checked
            {
                if (data is null) throw new ArgumentNullException(nameof(data));
                if (offset < 0 || offset > data.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                if (scanEndExclusive < 0)
                {
                    scanEndExclusive = data.Length;
                }

                scanEndExclusive = Math.Min(scanEndExclusive, data.Length);
                if (offset > scanEndExclusive)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                int pos = offset;
                while (pos < scanEndExclusive)
                {
                    if (pos + 4 > scanEndExclusive)
                    {
                        return pos - offset;
                    }

                    uint stringLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos, 4));
                    if (IsTermination(stringLength, hostTagKeyByte))
                    {
                        return pos - offset;
                    }

                    if (stringLength == 0)
                    {
                        return pos + 4 - offset;
                    }

                    if (stringLength > int.MaxValue - 4)
                    {
                        throw new InvalidOperationException(
                            $"Length-prefixed string table at offset 0x{offset:X} has unsupported string length {stringLength}.");
                    }

                    int recordBytes = 4 + (int)stringLength;
                    if (pos + recordBytes > scanEndExclusive)
                    {
                        throw new InvalidOperationException(
                            $"Length-prefixed string ({stringLength} bytes) at offset 0x{pos:X} extends past scan end " +
                            $"(scan ends at {scanEndExclusive}).");
                    }

                    pos += recordBytes;
                }

                return pos - offset;
            }
        }

        internal static void SkipContinuation(BinaryReader reader, byte hostTagKeyByte, long scanEndExclusive)
        {
            if (reader is null) throw new ArgumentNullException(nameof(reader));

            Stream stream = reader.BaseStream;
            while (stream.Position < scanEndExclusive)
            {
                if (stream.Position + 4 > scanEndExclusive)
                {
                    return;
                }

                long recordStart = stream.Position;
                uint stringLength = reader.ReadUInt32();
                if (IsTermination(stringLength, hostTagKeyByte))
                {
                    stream.Position = recordStart;
                    return;
                }

                if (stringLength == 0)
                {
                    return;
                }

                if (stringLength > int.MaxValue - 4)
                {
                    throw new InvalidOperationException(
                        $"Length-prefixed string table continuation at offset 0x{recordStart:X} has unsupported string length {stringLength}.");
                }

                long recordEnd = recordStart + 4L + stringLength;
                if (recordEnd > scanEndExclusive)
                {
                    throw new InvalidOperationException(
                        $"Length-prefixed string ({stringLength} bytes) at offset 0x{recordStart:X} extends past scan end " +
                        $"(scan ends at {scanEndExclusive:X}).");
                }

                stream.Position = recordEnd;
            }
        }

        private static bool IsTermination(uint stringLength, byte hostTagKeyByte) => stringLength > hostTagKeyByte;
    }
}
