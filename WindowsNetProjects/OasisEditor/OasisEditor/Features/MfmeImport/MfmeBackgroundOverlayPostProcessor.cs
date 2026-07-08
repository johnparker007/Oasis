using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OasisEditor.Features.MfmeImport;

internal static class MfmeBackgroundOverlayPostProcessor
{
    public static bool TryBakeDisplayOverlays(
        string backgroundPath,
        PanelElementModel background,
        IEnumerable<PanelElementModel> elements,
        string projectAssetsRoot,
        ICollection<string> copied,
        out string? updatedBackgroundPath,
        out string? error)
    {
        updatedBackgroundPath = null;
        error = null;

        try
        {
            var displayElements = elements
                .Where(IsBackgroundCutoutDisplay)
                .ToArray();

            if (displayElements.Length == 0)
            {
                return true;
            }

            var backgroundImage = LoadBgra32(backgroundPath);
            if (backgroundImage.Width <= 0 || backgroundImage.Height <= 0)
            {
                return true;
            }

            foreach (var displayElement in displayElements)
            {
                if (TryResolveProjectAssetPath(displayElement.SecondaryAssetPath, projectAssetsRoot) is { } overlayPath && File.Exists(overlayPath))
                {
                    var overlayImage = LoadBgra32(overlayPath);
                    CopyOverlayIntoBackground(backgroundImage, background, displayElement, overlayImage);
                }
                else
                {
                    ClearBackgroundRect(backgroundImage, background, displayElement);
                }
            }

            var outputPath = ResolveOutputPath(backgroundPath);
            SavePng(backgroundImage, outputPath);

            if (!string.Equals(outputPath, backgroundPath, StringComparison.OrdinalIgnoreCase))
            {
                updatedBackgroundPath = ToProjectRelativeAssetPath(outputPath, projectAssetsRoot);
                copied.Add(updatedBackgroundPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsBackgroundCutoutDisplay(PanelElementModel element)
    {
        return element.Kind is PanelElementKind.Reel or PanelElementKind.Alpha or PanelElementKind.SevenSegment or PanelElementKind.VfdDotMatrix;
    }

    private static void CopyOverlayIntoBackground(PixelBuffer backgroundImage, PanelElementModel background, PanelElementModel overlayElement, PixelBuffer overlayImage)
    {
        var destinationRect = GetElementDestinationRect(backgroundImage, background, overlayElement);
        if (destinationRect.Width <= 0 || destinationRect.Height <= 0)
        {
            return;
        }

        for (var y = 0; y < destinationRect.Height; y++)
        {
            var destinationY = destinationRect.Y + y;
            if (destinationY < 0 || destinationY >= backgroundImage.Height)
            {
                continue;
            }

            var sourceY = ScaleCoordinate(y, destinationRect.Height, overlayImage.Height);
            for (var x = 0; x < destinationRect.Width; x++)
            {
                var destinationX = destinationRect.X + x;
                if (destinationX < 0 || destinationX >= backgroundImage.Width)
                {
                    continue;
                }

                var sourceX = ScaleCoordinate(x, destinationRect.Width, overlayImage.Width);
                CopySourcePixelToDestination(backgroundImage, destinationX, destinationY, overlayImage, sourceX, sourceY);
            }
        }
    }

    private static void ClearBackgroundRect(PixelBuffer backgroundImage, PanelElementModel background, PanelElementModel displayElement)
    {
        var destinationRect = GetElementDestinationRect(backgroundImage, background, displayElement);
        if (destinationRect.Width <= 0 || destinationRect.Height <= 0)
        {
            return;
        }

        for (var y = 0; y < destinationRect.Height; y++)
        {
            var destinationY = destinationRect.Y + y;
            if (destinationY < 0 || destinationY >= backgroundImage.Height)
            {
                continue;
            }

            for (var x = 0; x < destinationRect.Width; x++)
            {
                var destinationX = destinationRect.X + x;
                if (destinationX < 0 || destinationX >= backgroundImage.Width)
                {
                    continue;
                }

                var destinationOffset = (destinationY * backgroundImage.Stride) + (destinationX * 4);
                backgroundImage.Pixels[destinationOffset + 0] = 0;
                backgroundImage.Pixels[destinationOffset + 1] = 0;
                backgroundImage.Pixels[destinationOffset + 2] = 0;
                backgroundImage.Pixels[destinationOffset + 3] = 0;
            }
        }
    }

    private static PixelRect GetElementDestinationRect(PixelBuffer backgroundImage, PanelElementModel background, PanelElementModel overlayElement)
    {
        if (background.Width <= 0 || background.Height <= 0 || overlayElement.Width <= 0 || overlayElement.Height <= 0)
        {
            return new PixelRect(0, 0, 0, 0);
        }

        var scaleX = backgroundImage.Width / background.Width;
        var scaleY = backgroundImage.Height / background.Height;
        var x = (int)Math.Round((overlayElement.X - background.X) * scaleX);
        var y = (int)Math.Round((overlayElement.Y - background.Y) * scaleY);
        var width = Math.Max(1, (int)Math.Round(overlayElement.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(overlayElement.Height * scaleY));
        return new PixelRect(x, y, width, height);
    }

    private static void CopySourcePixelToDestination(PixelBuffer destination, int destinationX, int destinationY, PixelBuffer source, int sourceX, int sourceY)
    {
        var destinationOffset = (destinationY * destination.Stride) + (destinationX * 4);
        var sourceOffset = (sourceY * source.Stride) + (sourceX * 4);

        destination.Pixels[destinationOffset + 0] = source.Pixels[sourceOffset + 0];
        destination.Pixels[destinationOffset + 1] = source.Pixels[sourceOffset + 1];
        destination.Pixels[destinationOffset + 2] = source.Pixels[sourceOffset + 2];
        destination.Pixels[destinationOffset + 3] = source.Pixels[sourceOffset + 3];
    }

    private static PixelBuffer LoadBgra32(string path)
    {
        if (TryLoadUncompressedBgra32Bmp(path, out var bmp))
        {
            return bmp;
        }

        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, frame.Palette, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        if (IsPalettized(frame.Format) && frame.Palette is not null)
        {
            ReapplyIndexedAlpha(frame, pixels, stride);
        }

        return new PixelBuffer(converted.PixelWidth, converted.PixelHeight, stride, pixels);
    }

    private static bool TryLoadUncompressedBgra32Bmp(string path, out PixelBuffer image)
    {
        image = default!;

        if (!string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
        {
            return false;
        }

        var pixelDataOffset = ReadInt32LittleEndian(bytes, 10);
        var dibHeaderSize = ReadInt32LittleEndian(bytes, 14);
        if (dibHeaderSize < 40 || bytes.Length < 14 + dibHeaderSize)
        {
            return false;
        }

        var width = ReadInt32LittleEndian(bytes, 18);
        var signedHeight = ReadInt32LittleEndian(bytes, 22);
        var planes = ReadUInt16LittleEndian(bytes, 26);
        var bitsPerPixel = ReadUInt16LittleEndian(bytes, 28);
        var compression = ReadInt32LittleEndian(bytes, 30);
        if (width <= 0 || signedHeight == 0 || signedHeight == int.MinValue || planes != 1 || bitsPerPixel != 32 || compression != 0 || pixelDataOffset < 0)
        {
            return false;
        }

        var height = Math.Abs(signedHeight);
        var stride = checked(width * 4);
        var pixelByteCount = (long)stride * height;
        var requiredLength = pixelDataOffset + pixelByteCount;
        if (pixelByteCount > int.MaxValue || requiredLength > bytes.Length)
        {
            return false;
        }

        var pixels = new byte[stride * height];
        var sourceTopDown = signedHeight < 0;
        for (var y = 0; y < height; y++)
        {
            var sourceY = sourceTopDown ? y : height - 1 - y;
            Buffer.BlockCopy(bytes, pixelDataOffset + (sourceY * stride), pixels, y * stride, stride);
        }

        image = new PixelBuffer(width, height, stride, pixels);
        return true;
    }

    private static int ReadInt32LittleEndian(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
    }

    private static ushort ReadUInt16LittleEndian(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool IsPalettized(PixelFormat format)
    {
        return format == PixelFormats.Indexed1
            || format == PixelFormats.Indexed2
            || format == PixelFormats.Indexed4
            || format == PixelFormats.Indexed8;
    }

    private static void ReapplyIndexedAlpha(BitmapSource frame, byte[] pixels, int stride)
    {
        var palette = frame.Palette.Colors;
        if (palette.Count == 0)
        {
            return;
        }

        var bitsPerPixel = frame.Format.BitsPerPixel;
        if (bitsPerPixel is not (1 or 2 or 4 or 8))
        {
            return;
        }

        var indexedStride = (frame.PixelWidth * bitsPerPixel + 7) / 8;
        var indices = new byte[indexedStride * frame.PixelHeight];
        frame.CopyPixels(indices, indexedStride, 0);

        for (var y = 0; y < frame.PixelHeight; y++)
        {
            for (var x = 0; x < frame.PixelWidth; x++)
            {
                var paletteIndex = ReadPaletteIndex(indices, indexedStride, bitsPerPixel, x, y);
                if (paletteIndex < 0 || paletteIndex >= palette.Count)
                {
                    continue;
                }

                var color = palette[paletteIndex];
                pixels[(y * stride) + (x * 4) + 3] = color.A;
            }
        }
    }

    private static int ReadPaletteIndex(byte[] indices, int stride, int bitsPerPixel, int x, int y)
    {
        if (bitsPerPixel == 8)
        {
            return indices[(y * stride) + x];
        }

        var bitIndex = x * bitsPerPixel;
        var byteIndex = (y * stride) + (bitIndex / 8);
        var bitOffset = bitIndex % 8;
        var shift = 8 - bitsPerPixel - bitOffset;
        var mask = (1 << bitsPerPixel) - 1;
        return (indices[byteIndex] >> shift) & mask;
    }

    private static int ScaleCoordinate(int value, int sourceSize, int destinationSize)
    {
        if (sourceSize <= 1 || destinationSize <= 1)
        {
            return 0;
        }

        return (int)Math.Round((double)value * (destinationSize - 1) / (sourceSize - 1));
    }

    private static string ResolveOutputPath(string backgroundPath)
    {
        if (string.Equals(Path.GetExtension(backgroundPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return backgroundPath;
        }

        var directory = Path.GetDirectoryName(backgroundPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(backgroundPath);
        var candidate = Path.Combine(directory, $"{baseName}.png");
        var suffix = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}_{suffix}.png");
            suffix++;
        }

        return candidate;
    }

    private static string? TryResolveProjectAssetPath(string? projectRelativePath, string projectAssetsRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath) || !projectRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = projectRelativePath["Assets/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(projectAssetsRoot, relativePath));
        return IsPathUnderRoot(fullPath, projectAssetsRoot) ? fullPath : null;
    }

    private static string ToProjectRelativeAssetPath(string fullPath, string projectAssetsRoot)
    {
        var relativePath = Path.GetRelativePath(projectAssetsRoot, fullPath).Replace('\\', '/');
        return $"Assets/{relativePath}";
    }

    private static bool IsPathUnderRoot(string path, string root)
    {
        return path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, root, StringComparison.OrdinalIgnoreCase);
    }

    private static void SavePng(PixelBuffer image, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(destinationPath);
        encoder.Save(stream);
    }

    private readonly record struct PixelRect(int X, int Y, int Width, int Height);

    private sealed class PixelBuffer(int width, int height, int stride, byte[] pixels)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int Stride { get; } = stride;
        public byte[] Pixels { get; } = pixels;
    }
}
