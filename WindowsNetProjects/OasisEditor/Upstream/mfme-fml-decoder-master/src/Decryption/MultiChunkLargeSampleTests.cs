using System;
using System.IO;
using Xunit;

namespace MfmeFmlDecoder.Decryption
{
    public sealed class MultiChunkLargeSampleTests
    {
        private const string DefaultFmlPath = @"d:\SampleFML\multi_chunk-samples\multi_chunk_large_sample.fml";
        private const string DefaultDecodedPath = @"d:\SampleFML\multi_chunk-samples\multi_chunk_large_sample_decoded.dat";

        [Fact]
        public void Multi_chunk_large_sample_decrypts_and_decompresses_to_expected_dat()
        {
            string fmlPath = Environment.GetEnvironmentVariable("MFME_DECODER_SMOKE_FML") ?? DefaultFmlPath;
            string decodedPath = Environment.GetEnvironmentVariable("MFME_DECODER_SMOKE_DECODED") ?? DefaultDecodedPath;

            Assert.True(
                File.Exists(fmlPath),
                $"Smoke test FML not found at '{fmlPath}'. Set MFME_DECODER_SMOKE_FML to override.");

            Assert.True(
                File.Exists(decodedPath),
                $"Smoke test decoded DAT not found at '{decodedPath}'. Set MFME_DECODER_SMOKE_DECODED to override.");

            byte[] fmlBytes = File.ReadAllBytes(fmlPath);
            byte[] expected = File.ReadAllBytes(decodedPath);

            byte[] actual = FmlDecryptor.Decrypt(fmlBytes);

            Assert.Equal(expected.Length, actual.Length);
            AssertBytesEqual(expected, actual);
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual)
        {
            int length = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < length; i++)
            {
                if (expected[i] == actual[i])
                    continue;

                Assert.Fail(
                    $"Decrypted output differs at offset 0x{i:X8} ({i}): " +
                    $"expected 0x{expected[i]:X2}, actual 0x{actual[i]:X2}.");
            }
        }
    }
}
