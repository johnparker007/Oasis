using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MfmeFmlDecoder.Decryption
{
    internal static class FmlDecryptor
    {
        private const string AltPassword = "b67de657";

        private static readonly byte[] s_allFfBlock =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        };

        public static byte[] Decrypt(byte[] fmlBytes, IProgress<double> progress = null)
        {
            if (fmlBytes == null || fmlBytes.Length < 148)
                throw new ArgumentException("FML data too short");

            string password = DerivePassword(fmlBytes);

            try
            {
                return DecryptAllChunks(fmlBytes, password, progress);
            }
            catch (Exception)
            {
                progress?.Report(0);
                return DecryptAllChunks(fmlBytes, AltPassword, progress);
            }
        }

        // Two file format phases exist:
        //   Phase 1 (legacy): static password "b67de657", last chunk's next_header_offset == EOF (no seed)
        //   Phase 2 (current): 4-byte seed at EOF, last chunk's next_header_offset == EOF - 4
        // We detect the phase by walking the chunk header chain and comparing the final
        // next_header_offset to the file length.
        private static string DerivePassword(byte[] fmlBytes)
        {
            uint lastNextOff = FindLastNextHeaderOffset(fmlBytes);

            if (lastNextOff == fmlBytes.Length)
                return AltPassword;

            if (lastNextOff == fmlBytes.Length - 4)
            {
                uint seed = BitConverter.ToUInt32(fmlBytes, fmlBytes.Length - 4);
                return Mix(seed).ToString("x8");
            }

            uint last4 = BitConverter.ToUInt32(fmlBytes, fmlBytes.Length - 4);
            return Mix(last4).ToString("x8");
        }

        // Walks the chunk header chain from offset 128, following each chunk's
        // next_header_offset field, and returns the final chunk's next_header_offset.
        private static uint FindLastNextHeaderOffset(byte[] fmlBytes)
        {
            int pos = 128;
            uint lastNextOff = 0;
            int maxChunks = 10000;

            while (pos + 16 <= fmlBytes.Length && maxChunks-- > 0)
            {
                uint nextOff = BitConverter.ToUInt32(fmlBytes, pos + 12);
                lastNextOff = nextOff;

                if (nextOff >= fmlBytes.Length || nextOff <= pos)
                    break;

                if (nextOff + 16 > fmlBytes.Length)
                    break;

                pos = (int)nextOff;
            }

            return lastNextOff;
        }

        private sealed class ChunkWorkItem
        {
            public int CipherStart;
            public int CipherLength;
            public int MaxOutputLength;
            public byte[] DecryptedData;
        }

        private static byte[] DecryptAllChunks(byte[] fmlBytes, string password, IProgress<double> progress)
        {
            byte[] aesKey = Ripemd128.ComputeHash(Encoding.ASCII.GetBytes(password));
            byte[] iv = EncryptEcb(aesKey, s_allFfBlock);
            List<ChunkWorkItem> chunks = BuildChunkList(fmlBytes);

            if (chunks.Count == 0)
                return Array.Empty<byte>();

            int completed = 0;
            if (chunks.Count == 1)
            {
                chunks[0].DecryptedData = DecryptChunk(fmlBytes, chunks[0], aesKey, iv);
                progress?.Report(1.0);
                return chunks[0].DecryptedData ?? Array.Empty<byte>();
            }

            Parallel.ForEach(
                chunks,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                chunk =>
                {
                    chunk.DecryptedData = DecryptChunk(fmlBytes, chunk, aesKey, iv);
                    if (progress != null)
                    {
                        int done = System.Threading.Interlocked.Increment(ref completed);
                        progress.Report(Math.Min(1.0, (double)done / chunks.Count));
                    }
                });

            progress?.Report(1.0);
            return ConcatenateChunks(chunks);
        }

        private static List<ChunkWorkItem> BuildChunkList(byte[] fmlBytes)
        {
            var chunks = new List<ChunkWorkItem>();
            uint csize = BitConverter.ToUInt32(fmlBytes, 128);
            uint dsize = BitConverter.ToUInt32(fmlBytes, 132);
            int position = 144;

            while (position < fmlBytes.Length - 4)
            {
                int ctLen = (int)Math.Min(csize, (uint)(fmlBytes.Length - position));

                chunks.Add(new ChunkWorkItem
                {
                    CipherStart = position,
                    CipherLength = ctLen,
                    MaxOutputLength = dsize > 0 ? (int)dsize : 0
                });

                position += ctLen;

                if (ctLen == 0)
                    break;

                if (position + 16 > fmlBytes.Length - 4)
                    break;

                csize = BitConverter.ToUInt32(fmlBytes, position);
                dsize = BitConverter.ToUInt32(fmlBytes, position + 4);
                position += 16;
            }

            return chunks;
        }

        private static byte[] DecryptChunk(
            byte[] fmlBytes,
            ChunkWorkItem chunk,
            byte[] aesKey,
            byte[] iv)
        {
            if (chunk.CipherLength <= 0)
                return Array.Empty<byte>();

            byte[] compressed = ArrayPool<byte>.Shared.Rent(chunk.CipherLength);
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.ECB;
                    aes.Padding = PaddingMode.None;
                    aes.Key = aesKey;

                    using ICryptoTransform decryptor = aes.CreateDecryptor();
                    using ICryptoTransform encryptor = aes.CreateEncryptor();
                    EclDecrypt(
                        fmlBytes,
                        chunk.CipherStart,
                        chunk.CipherLength,
                        decryptor,
                        encryptor,
                        iv,
                        compressed);
                }

                return TrimToMaxLength(
                    ZlibDecompress(compressed, chunk.CipherLength, chunk.MaxOutputLength),
                    chunk.MaxOutputLength);
            }
            finally
            {
                Array.Clear(compressed, 0, chunk.CipherLength);
                ArrayPool<byte>.Shared.Return(compressed);
            }
        }

        private static byte[] TrimToMaxLength(byte[] data, int maxLength)
        {
            if (maxLength <= 0 || data == null || data.Length <= maxLength)
                return data ?? Array.Empty<byte>();

            byte[] trimmed = new byte[maxLength];
            Buffer.BlockCopy(data, 0, trimmed, 0, maxLength);
            return trimmed;
        }

        private static byte[] ConcatenateChunks(List<ChunkWorkItem> chunks)
        {
            int totalLength = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                byte[] data = chunks[i].DecryptedData;
                if (data != null)
                    totalLength += data.Length;
            }

            if (totalLength == 0)
                return Array.Empty<byte>();

            byte[] output = new byte[totalLength];
            int writePos = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                byte[] data = chunks[i].DecryptedData;
                if (data == null || data.Length == 0)
                    continue;

                Buffer.BlockCopy(data, 0, output, writePos, data.Length);
                writePos += data.Length;
            }

            return output;
        }

        private static uint Mix(uint last4)
        {
            uint result = last4;
            const uint addr = 0x00C59A00;
            for (int i = 0; i < 100; i++)
            {
                uint x = addr ^ result;
                uint v4 = (x >> 24) | (x << 24) | (x & 0x00FFFF00);
                result = (v4 << 3) | (v4 >> 29);
            }

            return result;
        }

        private static byte[] EncryptEcb(byte[] key, byte[] plaintext)
        {
            using Aes aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] result = new byte[plaintext.Length];
            encryptor.TransformBlock(plaintext, 0, plaintext.Length, result, 0);
            return result;
        }

        private static void EclDecrypt(
            byte[] source,
            int offset,
            int length,
            ICryptoTransform decryptor,
            ICryptoTransform encryptor,
            byte[] iv,
            byte[] result)
        {
            byte[] fb_d = new byte[16];
            byte[] fb_b = new byte[16];
            byte[] dec = new byte[16];
            byte[] keystream = new byte[16];
            Buffer.BlockCopy(iv, 0, fb_d, 0, 16);

            int fullBlocks = length - (length % 16);
            int resultPos = 0;

            for (int i = 0; i < fullBlocks; i += 16)
            {
                int sourceIndex = offset + i;
                for (int j = 0; j < 16; j++)
                    fb_b[j] = (byte)(source[sourceIndex + j] ^ fb_d[j]);

                decryptor.TransformBlock(source, sourceIndex, 16, dec, 0);

                for (int j = 0; j < 16; j++)
                    result[resultPos++] = (byte)(dec[j] ^ fb_d[j]);

                byte[] tmp = fb_d;
                fb_d = fb_b;
                fb_b = tmp;
            }

            int rem = length % 16;
            if (rem > 0)
            {
                encryptor.TransformBlock(fb_d, 0, 16, keystream, 0);

                int tailStart = offset + fullBlocks;
                for (int j = 0; j < rem; j++)
                    result[resultPos++] = (byte)(source[tailStart + j] ^ keystream[j]);
            }
        }

        private static byte[] ZlibDecompress(byte[] compressed, int compressedLength, int maxOutputSize)
        {
            using MemoryStream output = new MemoryStream(Math.Max(maxOutputSize, 4096));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                using var compressedStream = new MemoryStream(compressed, 0, compressedLength, writable: false);
                using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress, leaveOpen: false);
                int bytesRead;
                int written = 0;
                while ((bytesRead = zlib.Read(buffer, 0, buffer.Length)) > 0)
                {
                    int allowed = bytesRead;
                    if (maxOutputSize > 0)
                    {
                        int remaining = maxOutputSize - written;
                        if (remaining <= 0)
                            break;

                        allowed = Math.Min(remaining, bytesRead);
                    }

                    output.Write(buffer, 0, allowed);
                    written += allowed;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (output.Length == 0)
                return Array.Empty<byte>();

            return output.ToArray();
        }
    }
}
