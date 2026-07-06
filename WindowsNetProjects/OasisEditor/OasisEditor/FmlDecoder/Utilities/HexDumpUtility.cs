using System;
using System.Text;

namespace MfmeFmlDecoder.Utilities
{
    public static class HexDumpUtility
    {
        /// <summary>
        /// Formats up to <paramref name="count"/> bytes from <paramref name="source"/> starting at
        /// <paramref name="startIndex"/> as space-separated uppercase hex (e.g. <c>19 01 49 15</c>).
        /// </summary>
        public static string FormatNextBytesHex(byte[] source, long startIndex, int count = 10)
        {
            if (source == null || source.Length == 0 || startIndex >= source.Length)
            {
                return "(no data)";
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "count must be > 0");
            }

            int start = checked((int)Math.Max(0, startIndex));
            int take = Math.Min(count, source.Length - start);
            var sb = new StringBuilder(take * 3);
            for (int i = 0; i < take; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(source[start + i].ToString("X2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Prints a coloured hexdump of <paramref name="windowBytes"/> from <paramref name="source"/>,
        /// centered on <paramref name="pivotIndex"/> (clamped to array bounds).
        /// </summary>
        /// <param name="source">Full source buffer.</param>
        /// <param name="pivotIndex">Problem point; display offsets remain absolute within <paramref name="source"/>.</param>
        /// <param name="windowBytes">Total bytes to show (default: 100).</param>
        public static void PrintHexDumpWindow(
            byte[] source,
            long pivotIndex,
            int windowBytes = 100,
            int bytesPerLine = 16,
            bool showAscii = true)
        {
            if (RunLog.Quiet)
            {
                return;
            }

            if (source == null || source.Length == 0)
            {
                WriteColored("No data to display.\n", ConsoleColor.DarkGray);
                return;
            }

            if (windowBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowBytes), "windowBytes must be > 0");
            }

            long half = windowBytes / 2;
            long start = pivotIndex - half;
            if (start < 0)
            {
                start = 0;
            }

            long end = start + windowBytes;
            if (end > source.Length)
            {
                end = source.Length;
                start = Math.Max(0, end - windowBytes);
            }

            int startIndex = checked((int)start);
            int length = checked((int)(end - start));
            byte[] window = new byte[length];
            Buffer.BlockCopy(source, startIndex, window, 0, length);
            PrintHexDump((uint)startIndex, window, maxBytes: 0, bytesPerLine, showAscii);
        }

        /// <summary>
        /// Prints a coloured hexdump of the provided byte array.
        /// </summary>
        /// <param name="displayOffset">Starting offset for display purposes (where this data is located in the original source)</param>
        /// <param name="data">The byte array containing the data to dump</param>
        /// <param name="maxBytes">Maximum number of bytes to emit (default: 255). Pass 0 for unlimited.</param>
        /// <param name="bytesPerLine">Number of bytes to display per line (default: 16)</param>
        /// <param name="showAscii">Whether to show ASCII representation (default: true)</param>
        public static void PrintHexDump(uint displayOffset, byte[] data, int maxBytes = 255, int bytesPerLine = 16, bool showAscii = true)
        {
            if (RunLog.Quiet)
            {
                return;
            }

            if (data == null || data.Length == 0)
            {
                WriteColored("No data to display.\n", ConsoleColor.DarkGray);
                return;
            }

            if (bytesPerLine <= 0)
                throw new ArgumentOutOfRangeException(nameof(bytesPerLine), "bytesPerLine must be > 0");

            if (maxBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "maxBytes must be >= 0 (0 means unlimited)");

            int effectiveLength = (maxBytes == 0) ? data.Length : Math.Min(data.Length, maxBytes);
            int totalLines = (effectiveLength + bytesPerLine - 1) / bytesPerLine;

            for (int line = 0; line < totalLines; line++)
            {
                int lineOffset  = (int)displayOffset + (line * bytesPerLine);
                int dataIndex   = line * bytesPerLine;
                int bytesInLine = Math.Min(bytesPerLine, effectiveLength - dataIndex);

                // Offset column
                WriteColored($"{lineOffset:X8}", ConsoleColor.Cyan);
                WriteColored("  ", ConsoleColor.DarkGray);

                // Hex bytes
                for (int i = 0; i < bytesPerLine; i++)
                {
                    if (i < bytesInLine)
                    {
                        byte b = data[dataIndex + i];
                        WriteColored($"{b:X2} ", ByteColor(b));
                    }
                    else
                    {
                        RunLog.Write("   ");
                    }

                    if (i == 7)
                        WriteColored(" ", ConsoleColor.DarkGray);
                }

                // ASCII column
                if (showAscii)
                {
                    WriteColored(" |", ConsoleColor.DarkGray);
                    for (int i = 0; i < bytesInLine; i++)
                    {
                        byte b = data[dataIndex + i];
                        if (b >= 32 && b <= 126)
                            WriteColored(((char)b).ToString(), ConsoleColor.Green);
                        else
                            WriteColored(".", ConsoleColor.DarkGray);
                    }
                    WriteColored("|", ConsoleColor.DarkGray);
                }

                RunLog.WriteLine();
            }

            if (effectiveLength < data.Length)
            {
                WriteColored($"... truncated: showing {effectiveLength} of {data.Length} bytes\n", ConsoleColor.DarkYellow);
            }
        }

        /// <summary>
        /// Plain-text hexdump to diagnostic output (stderr in quiet/json mode). Not gated on <see cref="RunLog.Quiet"/>.
        /// </summary>
        public static void WriteDiagnosticHexDumpWindow(
            byte[] source,
            long pivotIndex,
            int windowBytes = 100,
            int bytesPerLine = 16)
        {
            if (source == null || source.Length == 0)
            {
                RunLog.WriteDiagnosticLine("(no data)");
                return;
            }

            if (windowBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowBytes), "windowBytes must be > 0");
            }

            if (bytesPerLine <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesPerLine), "bytesPerLine must be > 0");
            }

            long half = windowBytes / 2;
            long start = pivotIndex - half;
            if (start < 0)
            {
                start = 0;
            }

            long end = start + windowBytes;
            if (end > source.Length)
            {
                end = source.Length;
                start = Math.Max(0, end - windowBytes);
            }

            int startIndex = checked((int)start);
            int length = checked((int)(end - start));
            int totalLines = (length + bytesPerLine - 1) / bytesPerLine;

            for (int line = 0; line < totalLines; line++)
            {
                int lineOffset = startIndex + (line * bytesPerLine);
                int dataIndex = line * bytesPerLine;
                int bytesInLine = Math.Min(bytesPerLine, length - dataIndex);

                var sb = new StringBuilder(80);
                sb.Append(lineOffset.ToString("X8"));
                sb.Append("  ");

                for (int i = 0; i < bytesPerLine; i++)
                {
                    if (i == 8)
                    {
                        sb.Append(' ');
                    }

                    if (i < bytesInLine)
                    {
                        sb.Append(source[startIndex + dataIndex + i].ToString("X2"));
                    }
                    else
                    {
                        sb.Append("  ");
                    }

                    sb.Append(' ');
                }

                sb.Append(" |");
                for (int i = 0; i < bytesInLine; i++)
                {
                    byte b = source[startIndex + dataIndex + i];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                sb.Append('|');
                RunLog.WriteDiagnosticLine(sb.ToString());
            }

            if (length < windowBytes && startIndex + length < source.Length)
            {
                RunLog.WriteDiagnosticLine($"... showing {length} bytes around pivot 0x{pivotIndex:X}");
            }
        }

        private static ConsoleColor ByteColor(byte b)
        {
            if (b == 0x00)                       return ConsoleColor.DarkGray;
            if (b >= 0x20 && b <= 0x7E)          return ConsoleColor.White;
            if (b <= 0x1F || b == 0x7F)          return ConsoleColor.Red;
            return ConsoleColor.Yellow;
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            RunLog.WriteColored(text, color);
        }
    }
}
