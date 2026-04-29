using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace MfmeFmlDecoder;

/// <summary>
/// Experimental native decoder for MFME .fml files written through ComponentAce EasyCompression.
/// Requires NuGet package: Portable.BouncyCastle
/// </summary>
public static class MfmeLegacyLayoutDecoder
{
    private const uint XorConst = 0x00C59A00;
    private const string FallbackPassword = "b67de657";
    private const int EclHeaderSize = 0x90;

    public static byte[] DecodeFile(string path)
        => Decode(File.ReadAllBytes(path));

    public static byte[] Decode(ReadOnlySpan<byte> fml)
    {
        if (fml.Length < EclHeaderSize + 4)
            throw new InvalidDataException("File is too small to be an MFME/EasyCompression layout.");

        if (fml[0] != (byte)'A' || fml[1] != (byte)'A' || fml[2] != (byte)'C' || fml[3] != (byte)'S')
            throw new InvalidDataException("Missing EasyCompression AACS header.");

        uint encryptedPayloadLength = BinaryPrimitives.ReadUInt32LittleEndian(fml.Slice(0x80, 4));
        uint decompressedLength = BinaryPrimitives.ReadUInt32LittleEndian(fml.Slice(0x84, 4));
        uint eclStreamLength = BinaryPrimitives.ReadUInt32LittleEndian(fml.Slice(0x8C, 4));

        if (eclStreamLength + 4 != fml.Length)
            throw new InvalidDataException($"Unexpected stream length. Header says {eclStreamLength}+4, file is {fml.Length}.");

        if (EclHeaderSize + encryptedPayloadLength > eclStreamLength)
            throw new InvalidDataException("Encrypted payload length is outside the ECL stream.");

        ReadOnlySpan<byte> encryptedPayload = fml.Slice(EclHeaderSize, checked((int)encryptedPayloadLength));

        string derived = DerivePassword(fml);
        Exception? firstFailure = null;

        foreach (string password in new[] { derived, FallbackPassword }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                byte[] compressed = DecryptEclPayload_DecRijndael(password, encryptedPayload);
                byte[] decoded = InflateZlib(compressed);

                if (decoded.Length != decompressedLength)
                    throw new InvalidDataException($"Decoded length mismatch for password {password}: got {decoded.Length}, expected {decompressedLength}.");

                return decoded;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                firstFailure ??= ex;
            }
        }

        throw new InvalidDataException("Could not decode MFME layout with derived or fallback password.", firstFailure);
    }

    public static string DerivePassword(ReadOnlySpan<byte> file)
    {
        uint seed = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(file.Length - 4, 4));
        return Mixer(XorConst, seed).ToString("x8");
    }

    private static uint Mixer(uint a1, uint a2)
    {
        uint result = a2;
        for (int i = 0; i < 100; i++)
        {
            uint x = a1 ^ result;
            uint v4 = (x >> 24) | (x << 24) | (x & 0x00FFFF00);
            result = (v4 << 3) | (v4 >> 29);
        }
        return result;
    }

    private static byte[] InflateZlib(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Implements the DEC v3-style InitKey + Rijndael + CTS mode that EasyCompression appears to use.
    /// If this does not validate against a file, the remaining difference is in ECL's exact key/IV/mode choice.
    /// </summary>
    private static byte[] DecryptEclPayload_DecRijndael(string password, ReadOnlySpan<byte> ciphertext)
    {
        byte[] key = RipeMd256(System.Text.Encoding.ASCII.GetBytes(password));

        var engine = new RijndaelEngine(128);
        engine.Init(true, new KeyParameter(key));

        byte[] vector = Enumerable.Repeat((byte)0xFF, 16).ToArray();
        byte[] encryptedVector = new byte[16];
        engine.ProcessBlock(vector, 0, encryptedVector, 0);
        byte[] feedback = encryptedVector;

        var decryptEngine = new RijndaelEngine(128);
        decryptEngine.Init(false, new KeyParameter(key));

        byte[] dest = ciphertext.ToArray();
        byte[] buffer = new byte[16];
        byte[] f = feedback.ToArray();
        byte[] b = buffer;
        int offset = 0;
        int remaining = dest.Length;

        while (remaining >= 16)
        {
            XorBlock(dest, offset, f, 0, b, 0, 16);
            decryptEngine.ProcessBlock(dest, offset, dest, offset);
            XorInPlace(dest, offset, f, 0, 16);

            (b, f) = (f, b);
            offset += 16;
            remaining -= 16;
        }

        if (!ReferenceEquals(f, feedback))
            Array.Copy(f, feedback, 16);

        if (remaining > 0)
        {
            var enc = new RijndaelEngine(128);
            enc.Init(true, new KeyParameter(key));
            byte[] stream = new byte[16];
            enc.ProcessBlock(feedback, 0, stream, 0);

            for (int i = 0; i < remaining; i++)
                dest[offset + i] ^= stream[i];
        }

        // zlib streams from the observed decoded files start 78 5E at compression level 5.
        if (dest.Length < 2 || dest[0] != 0x78)
            throw new InvalidDataException("Decryption did not produce a zlib stream.");

        return dest;
    }

    private static byte[] RipeMd256(byte[] data)
    {
        var d = new RipeMD256Digest();
        d.BlockUpdate(data, 0, data.Length);
        byte[] hash = new byte[d.GetDigestSize()];
        d.DoFinal(hash, 0);
        return hash;
    }

    private static void XorBlock(byte[] a, int ao, byte[] b, int bo, byte[] dst, int dstO, int len)
    {
        for (int i = 0; i < len; i++)
            dst[dstO + i] = (byte)(a[ao + i] ^ b[bo + i]);
    }

    private static void XorInPlace(byte[] a, int ao, byte[] b, int bo, int len)
    {
        for (int i = 0; i < len; i++)
            a[ao + i] ^= b[bo + i];
    }
}
