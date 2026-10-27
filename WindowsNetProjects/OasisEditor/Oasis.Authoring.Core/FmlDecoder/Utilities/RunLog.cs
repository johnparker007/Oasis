using System;

namespace MfmeFmlDecoder.Utilities
{
    internal static class RunLog
    {
        public static bool Quiet { get; set; }

        public static void Write(string value)
        {
            if (Quiet) return;
            Console.Write(value);
        }

        public static void WriteLine(string value)
        {
            if (Quiet) return;
            Console.WriteLine(value);
        }

        public static void WriteLine()
        {
            if (Quiet) return;
            Console.WriteLine();
        }

        /// <summary>
        /// Writes a line of diagnostic output. When <see cref="Quiet"/> is true (for example
        /// <c>--json</c> mode, where stdout must contain only JSON), writes to standard error
        /// so logs stay visible without corrupting the primary output stream.
        /// </summary>
        public static void WriteDiagnosticLine(string value)
        {
            if (Quiet)
            {
                Console.Error.WriteLine(value);
            }
            else
            {
                Console.WriteLine(value);
            }
        }

        public static void WriteColored(string text, ConsoleColor color)
        {
            if (Quiet) return;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        public static void WriteErrorLine(string value)
        {
            Console.Error.WriteLine(value);
        }
    }
}
