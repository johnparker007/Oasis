using System;
using System.Collections.Generic;
using System.IO;
using MfmeFmlDecoder.Decryption;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component;
using MfmeFmlDecoder.Utilities;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.Application
{
    internal sealed class Application
    {
        public Application()
        {
        }

        public int Run(string[] args)
        {
            if (!TryParseArgs(
                    args,
                    out string fileName,
                    out uint offset,
                    out bool writeLayoutJson,
                    out bool exportLayout,
                    out string exportOutputPath,
                    out bool printMfmeVersion,
                    out string parseError))
            {
                if (!string.IsNullOrEmpty(parseError))
                    RunLog.WriteErrorLine(parseError);
                PrintUsage();
                return 1;
            }

            RunLog.Quiet = writeLayoutJson || exportLayout || printMfmeVersion;
            try
            {
                string inputPath = Path.GetFullPath(fileName);
                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException(inputPath);
                }

                if (printMfmeVersion)
                {
                    string version = ReadMfmeVersion(inputPath, offset);
                    Console.Out.WriteLine(version);
                    return 0;
                }

                var componentParser = new ComponentParser();
                var fileWalker = new FileWalker(
                    new ComponentWalker(
                        componentParser
                    )
                );

                if (string.Equals(Path.GetExtension(inputPath), ".fml", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] decrypted = FmlDecryptor.Decrypt(ReadFileBytes(inputPath));
                    fileWalker.WalkTlv(decrypted, offset);
                }
                else
                {
                    fileWalker.WalkTlv(inputPath, offset);
                }

                if (writeLayoutJson || exportLayout)
                {
                    var layout = componentParser.ToLayout();

                    if (writeLayoutJson)
                    {
                        string json = layout.ToJson(indented: true);
                        Console.Out.Write(json);
                    }

                    if (exportLayout)
                    {
                        string outputZipPath = exportOutputPath ?? Path.ChangeExtension(inputPath, ".zip");
                        LayoutExporter.ExportToZip(layout, outputZipPath);
                        RunLog.WriteDiagnosticLine($"Exported layout to {Path.GetFullPath(outputZipPath)}");
                    }
                }

                return 0;
            }
            catch (FileNotFoundException ex)
            {
                RunLog.WriteErrorLine($"Error: File not found - {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                RunLog.WriteErrorLine($"Error: {ex.Message}");
                return 1;
            }
            finally
            {
                RunLog.Quiet = false;
            }
        }

        private static string ReadMfmeVersion(string inputPath, uint offset)
        {
            if (string.Equals(Path.GetExtension(inputPath), ".fml", StringComparison.OrdinalIgnoreCase))
            {
                byte[] decrypted = FmlDecryptor.Decrypt(ReadFileBytes(inputPath));
                return MfmeVersionReader.Read(decrypted, offset);
            }

            return MfmeVersionReader.Read(inputPath, offset);
        }

        private static bool TryParseArgs(
            string[] args,
            out string fileName,
            out uint offset,
            out bool writeLayoutJson,
            out bool exportLayout,
            out string exportOutputPath,
            out bool printMfmeVersion,
            out string error)
        {
            fileName = null;
            offset = 0;
            writeLayoutJson = false;
            exportLayout = false;
            exportOutputPath = null;
            printMfmeVersion = false;
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
                else if (a == "--export")
                {
                    exportLayout = true;
                }
                else if (a.StartsWith("--export=", StringComparison.Ordinal))
                {
                    string value = a.Substring("--export=".Length).Trim();
                    if (value.Length == 0)
                    {
                        error = "Export output path must not be empty when using --export=.";
                        return false;
                    }

                    exportLayout = true;
                    exportOutputPath = value;
                }
                else if (a == "--mfme-version")
                {
                    printMfmeVersion = true;
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

            if (printMfmeVersion && (writeLayoutJson || exportLayout))
            {
                error = "--mfme-version cannot be combined with --json or --export.";
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

        private static byte[] ReadFileBytes(string fullInputPath)
        {
            using FileStream stream = new FileStream(
                fullInputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan);

            if (stream.Length == 0)
                return Array.Empty<byte>();

            if (stream.Length > int.MaxValue)
                throw new InvalidOperationException($"File is too large: {fullInputPath}");

            byte[] buffer = new byte[(int)stream.Length];
            int readOffset = 0;
            int remaining = buffer.Length;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, readOffset, remaining);
                if (read == 0)
                {
                    throw new EndOfStreamException(
                        $"Unexpected EOF reading file '{fullInputPath}' (read {readOffset} of {buffer.Length} bytes).");
                }

                readOffset += read;
                remaining -= read;
            }

            return buffer;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  MfmeFmlDecoder <filename.(dat|fml)> [offset] [--json] [--export]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --json, -j          Emit decoded layout as JSON on standard output only; errors go to stderr.");
            Console.WriteLine("  --export[=path]     Write layout.json and component images to a zip file.");
            Console.WriteLine("                      Default output: <input-file>.zip in the same directory as the input file.");
            Console.WriteLine("  --mfme-version      Print the MFME version from the layout file (TLV tag 0x2F) on standard output.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  MfmeFmlDecoder file.dat --json");
            Console.WriteLine("  MfmeFmlDecoder file.dat --json > layout.json");
            Console.WriteLine("  MfmeFmlDecoder file.dat 0x1000 --json");
            Console.WriteLine("  MfmeFmlDecoder file.dat --export");
            Console.WriteLine("  MfmeFmlDecoder file.dat --export=C:\\out\\layout.zip");
            Console.WriteLine("  MfmeFmlDecoder file.fml --mfme-version");
        }

    }
}
