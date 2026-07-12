using System;
using System.IO;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.Utilities;

namespace MfmeFmlDecoder.Decoder
{
    internal sealed class FileWalker
    {
        private const uint TlvTerminationTag = 0xFFFFFFFF;
        private const string SupportedMfmeVersion = "20.1";

        private ComponentWalker _componentWalker;
        public FileWalker(ComponentWalker componentWalker)
        {
            _componentWalker = componentWalker;
        }

        public void WalkTlv(string fileName, uint offset)
        {
            using FileStream fileStream = new FileStream(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan);
            WalkTlv(fileStream, offset);
        }

        public void WalkTlv(byte[] data, uint offset)
        {
            using MemoryStream fileStream = new MemoryStream(data, writable: false);
            WalkTlv(fileStream, offset);
        }

        private void WalkTlv(Stream fileStream, uint offset)
        {
            using BinaryReader reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (offset > fileStream.Length)
            {
                throw new InvalidOperationException(
                    $"Offset 0x{offset:X} is beyond file length 0x{fileStream.Length:X}.");
            }

            if (offset > 0)
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
            }

            while (fileStream.Position < fileStream.Length)
            {
                long recordStartOffset = fileStream.Position;

                uint tag = reader.ReadUInt32();
                uint length = reader.ReadUInt32();
                byte[] values = reader.ReadBytes(checked((int)length));
                if (values.Length != length)
                {
                    throw new EndOfStreamException(
                        $"Unexpected EOF reading tag 0x{tag:X2} at offset 0x{recordStartOffset:X8}. " +
                        $"Expected {length} bytes but only {values.Length} bytes were read.");
                }

                OnRecordRead(recordStartOffset, tag, length, values);

                if (tag == MfmeVersionReader.MfmeVersionTag)
                {
                    ValidateMfmeVersion(recordStartOffset, values);
                }

                if (LengthPrefixedStringTableScanner.IsFileLevelTag(tag))
                {
                    LengthPrefixedStringTableScanner.SkipContinuation(
                        reader,
                        hostTagKeyByte: (byte)tag,
                        scanEndExclusive: fileStream.Length);
                }

                if (tag == TlvTerminationTag)
                {
                    long currentOffset = fileStream.Position;

                    RunLog.WriteLine();
                    RunLog.WriteLine($"TLV Termination tag 0xFFFFFFFF found at offset 0x{recordStartOffset:X8}");
                    RunLog.WriteLine($"Current offset: 0x{currentOffset:X8}");

                    _componentWalker.WalkComponents(fileStream, reader, currentOffset);
                    break;
                }
            }
        }

        private void OnRecordRead(long offset, uint tag, uint length, byte[] values)
        {
            RunLog.WriteLine($"Offset: 0x{offset:X8}, Tag: 0x{tag:X2}, Length: 0x{length:X2}");
            HexDumpUtility.PrintHexDump((uint)(offset + 8), values, maxBytes: 0xFF);
        }

        private static void ValidateMfmeVersion(long recordStartOffset, byte[] values)
        {
            string version = MfmeVersionReader.FormatVersion(values);
            if (string.Equals(version, SupportedMfmeVersion, StringComparison.Ordinal))
            {
                return;
            }

            throw new UnsupportedMfmeVersionException(recordStartOffset, values);
        }
    }
}
