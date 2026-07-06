using System;
using System.Collections.Generic;
using MfmeFmlDecoder.Utilities;

namespace MfmeFmlDecoder.Application
{
    internal sealed class Application
    {
        public Application()
        {
        }

        public int Run(string[] args)
        {
            if (!TryParseArgs(args, out string fileName, out uint offset, out bool writeLayoutJson, out string parseError))
            {
                if (!string.IsNullOrEmpty(parseError))
                    RunLog.WriteErrorLine(parseError);
                PrintUsage();
                return 1;
            }

            RunLog.Quiet = writeLayoutJson;
            try
            {
                var result = new FmlDecoderService().DecodeToJson(fileName, offset);
                foreach (var warning in result.Warnings)
                {
                    RunLog.WriteErrorLine($"Warning: {warning}");
                }

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        RunLog.WriteErrorLine($"Error: {error}");
                    }

                    return 1;
                }

                if (writeLayoutJson)
                {
                    Console.Out.Write(result.Json);
                }

                return 0;
            }
            finally
            {
                RunLog.Quiet = false;
            }
        }

        private static bool TryParseArgs(string[] args, out string fileName, out uint offset, out bool writeLayoutJson, out string error)
        {
            fileName = null;
            offset = 0;
            writeLayoutJson = false;
            error = null;

            var positionals = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--json" || string.Equals(a, "-j", StringComparison.OrdinalIgnoreCase))
                {
                    writeLayoutJson = true;
                }
                else if (a.StartsWith("--json=", StringComparison.Ordinal))
                {
                    string value = a.Substring("--json=".Length).Trim();
                    if (value.Length > 0)
                    {
                        error =
                            "Layout JSON is written to standard output only; do not pass a path in --json=. " +
                            "Use shell redirection (example: MfmeFmlDecoder file.dat --json > layout.json).";
                        return false;
                    }

                    writeLayoutJson = true;
                }
                else if (!a.StartsWith("-", StringComparison.Ordinal))
                {
                    positionals.Add(a);
                }
                else
                {
                    error = $"Unknown option: {a}";
                    return false;
                }
            }

            if (positionals.Count == 0)
            {
                return false;
            }

            fileName = positionals[0];
            if (positionals.Count >= 2)
            {
                if (!TryParseOffset(positionals[1], out offset))
                {
                    error =
                        $"Invalid offset '{positionals[1]}'. Layout JSON is written to standard output only " +
                        "(use shell redirection to save a file).";
                    return false;
                }
            }

            if (positionals.Count > 2)
            {
                error = "Too many positional arguments (expected <file> [offset]).";
                return false;
            }

            return true;
        }

        private static bool TryParseOffset(string s, out uint offset)
        {
            offset = 0;
            if (string.IsNullOrWhiteSpace(s))
            {
                return true;
            }

            try
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    offset = Convert.ToUInt32(s.Substring(2), 16);
                }
                else
                {
                    offset = uint.Parse(s);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  MfmeFmlDecoder <filename.(dat|fml)> [offset] [--json]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --json, -j          Emit decoded layout as JSON on standard output only; errors go to stderr.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  MfmeFmlDecoder file.dat --json");
            Console.WriteLine("  MfmeFmlDecoder file.dat --json > layout.json");
            Console.WriteLine("  MfmeFmlDecoder file.dat 0x1000 --json");
        }

    }
}
