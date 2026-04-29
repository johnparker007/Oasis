using MfmeFmlDecoder;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: MfmeFmlDecoder <path-to-file.fml>");
    return 1;
}

string inputPath = args[0];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"File not found: {inputPath}");
    return 1;
}

try
{
    byte[] decodedBytes = MfmeLegacyLayoutDecoder.DecodeFile(inputPath);
    string outputPath = Path.ChangeExtension(inputPath, ".decoded");

    File.WriteAllBytes(outputPath, decodedBytes);
    Console.WriteLine($"Decoded {decodedBytes.Length} bytes to: {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Decoding failed: {ex.Message}");
    return 1;
}
