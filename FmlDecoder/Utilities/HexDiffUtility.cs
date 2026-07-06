using System;

namespace MfmeFmlDecoder.Utilities
{
    internal static class HexDiffUtility
    {
        public static void PrintHexDiff(
            uint displayOffset,
            byte[] left,
            byte[] right,
            string leftLabel = "A",
            string rightLabel = "B",
            int bytesPerLine = 16,
            bool showAscii = true
        )
        {
            if (RunLog.Quiet)
            {
                return;
            }

            if (left is null) throw new ArgumentNullException(nameof(left));
            if (right is null) throw new ArgumentNullException(nameof(right));
            if (bytesPerLine <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerLine));

            int maxLen = Math.Max(left.Length, right.Length);
            int totalLines = (maxLen + bytesPerLine - 1) / bytesPerLine;

            for (int line = 0; line < totalLines; line++)
            {
                int lineStart = line * bytesPerLine;
                int bytesInLine = Math.Min(bytesPerLine, maxLen - lineStart);
                uint lineOffset = displayOffset + (uint)lineStart;

                PrintOneSide(lineOffset, leftLabel, left, right, lineStart, bytesInLine, bytesPerLine, showAscii, isLeft: true);
                PrintOneSide(lineOffset, rightLabel, right, left, lineStart, bytesInLine, bytesPerLine, showAscii, isLeft: false);
                RunLog.WriteLine();
            }
        }

        private static void PrintOneSide(
            uint offset,
            string label,
            byte[] primary,
            byte[] other,
            int lineStart,
            int bytesInLine,
            int bytesPerLine,
            bool showAscii,
            bool isLeft
        )
        {
            WriteColored($"{label} ", ConsoleColor.DarkGray);
            WriteColored($"{offset:X8}", ConsoleColor.Cyan);
            WriteColored("  ", ConsoleColor.DarkGray);

            for (int i = 0; i < bytesPerLine; i++)
            {
                int idx = lineStart + i;
                if (i < bytesInLine && idx < primary.Length)
                {
                    byte b = primary[idx];
                    bool differs = idx >= other.Length || other[idx] != b;
                    WriteColored($"{b:X2} ", differs ? (isLeft ? ConsoleColor.Red : ConsoleColor.Yellow) : ConsoleColor.DarkGray);
                }
                else
                {
                    RunLog.Write("   ");
                }

                if (i == 7) WriteColored(" ", ConsoleColor.DarkGray);
            }

            if (showAscii)
            {
                WriteColored(" |", ConsoleColor.DarkGray);
                for (int i = 0; i < bytesInLine; i++)
                {
                    int idx = lineStart + i;
                    if (idx >= primary.Length)
                    {
                        WriteColored(" ", ConsoleColor.DarkGray);
                        continue;
                    }

                    byte b = primary[idx];
                    bool differs = idx >= other.Length || other[idx] != b;
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    WriteColored(c.ToString(), differs ? (isLeft ? ConsoleColor.Red : ConsoleColor.Yellow) : ConsoleColor.DarkGray);
                }
                WriteColored("|", ConsoleColor.DarkGray);
            }

            RunLog.WriteLine();
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            RunLog.WriteColored(text, color);
        }
    }
}
