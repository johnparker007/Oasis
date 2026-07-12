using System;
using System.IO;
using System.Text;

namespace MfmeFmlDecoder.Decoder
{
    internal static class MfmeVersionReader
    {
        internal const uint MfmeVersionTag = 0x2F;

        public static string Read(string fileName, uint offset)
        {
            using FileStream fileStream = new FileStream(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan);
            return Read(fileStream, offset);
        }

        public static string Read(byte[] data, uint offset)
        {
            using MemoryStream fileStream = new MemoryStream(data, writable: false);
            return Read(fileStream, offset);
        }

        public static string Read(Stream stream, uint offset)
        {
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (offset > stream.Length)
            {
                throw new InvalidOperationException(
                    $"Offset 0x{offset:X} is beyond file length 0x{stream.Length:X}.");
            }

            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }

            while (stream.Position < stream.Length)
            {
                long recordStartOffset = stream.Position;

                uint tag = reader.ReadUInt32();
                uint length = reader.ReadUInt32();
                byte[] values = reader.ReadBytes(checked((int)length));
                if (values.Length != length)
                {
                    throw new EndOfStreamException(
                        $"Unexpected EOF reading tag 0x{tag:X2} at offset 0x{recordStartOffset:X8}. " +
                        $"Expected {length} bytes but only {values.Length} bytes were read.");
                }

                if (tag == MfmeVersionTag)
                {
                    return FormatVersion(values);
                }

                if (LengthPrefixedStringTableScanner.IsFileLevelTag(tag))
                {
                    LengthPrefixedStringTableScanner.SkipContinuation(
                        reader,
                        hostTagKeyByte: (byte)tag,
                        scanEndExclusive: stream.Length);
                }
            }

            throw new InvalidOperationException(
                $"MFME version tag 0x{MfmeVersionTag:X2} was not found before end of file.");
        }

        internal static string FormatVersion(byte[] versionBytes)
        {
            if (versionBytes is null || versionBytes.Length == 0)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(versionBytes);
        }
    }
}
