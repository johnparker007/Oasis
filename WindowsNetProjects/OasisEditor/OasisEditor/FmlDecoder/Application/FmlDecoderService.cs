using System;
using System.Collections.Generic;
using System.IO;
using MfmeFmlDecoder.Decryption;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.src.Decoder.Component;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.Application
{
    public sealed class FmlDecodeResult
    {
        public bool Succeeded => Errors.Count == 0 && !string.IsNullOrEmpty(Json);
        public string Json { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        private FmlDecodeResult(string json, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Json = json;
            Errors = errors;
            Warnings = warnings;
        }

        public static FmlDecodeResult Success(string json, IReadOnlyList<string> warnings)
            => new FmlDecodeResult(json, Array.Empty<string>(), warnings ?? Array.Empty<string>());

        public static FmlDecodeResult Failure(IReadOnlyList<string> errors, IReadOnlyList<string>? warnings = null)
            => new FmlDecodeResult(string.Empty, errors ?? Array.Empty<string>(), warnings ?? Array.Empty<string>());
    }

    public sealed class FmlDecoderService
    {
        public FmlDecodeResult DecodeToJson(string inputPath, uint offset = 0)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputPath))
                {
                    return FmlDecodeResult.Failure(new[] { "Input path is required." });
                }

                string fullInputPath = Path.GetFullPath(inputPath);
                if (!File.Exists(fullInputPath))
                {
                    return FmlDecodeResult.Failure(new[] { $"File not found: {fullInputPath}" });
                }

                var componentParser = new ComponentParser();
                var fileWalker = new FileWalker(new ComponentWalker(componentParser));

                if (string.Equals(Path.GetExtension(fullInputPath), ".fml", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] decrypted = FmlDecryptor.Decrypt(ReadFileBytes(fullInputPath));
                    fileWalker.WalkTlv(decrypted, offset);
                }
                else
                {
                    fileWalker.WalkTlv(fullInputPath, offset);
                }

                string json = componentParser.ToLayout().ToJson(indented: true);
                return FmlDecodeResult.Success(json, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                return FmlDecodeResult.Failure(new[] { ex.Message });
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
    }
}
