using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OasisEditor.Features.LayoutImport;

internal static class MfmeBackgroundOverlayPostProcessor
{
    public static bool TryNormalizeBackground(
        string backgroundPath,
        PanelElementModel background,
        string projectAssetsRoot,
        ICollection<string> copied,
        out string? updatedBackgroundPath,
        out string? error)
    {
        updatedBackgroundPath = null;
        error = null;

        try
        {
            var width = (int)Math.Round(background.Width);
            var height = (int)Math.Round(background.Height);
            if (width <= 0 || height <= 0)
            {
                return true;
            }

            var source = LoadBgra32(backgroundPath);
            var normalized = new PixelBuffer(width, height, checked(width * 4), new byte[checked(width * height * 4)]);
            var sourceX = (int)Math.Min(source.Width, Math.Max(0L, -(long)background.SourceImageOffsetX));
            var sourceY = (int)Math.Min(source.Height, Math.Max(0L, -(long)background.SourceImageOffsetY));
            var destinationX = (int)Math.Min(normalized.Width, Math.Max(0L, background.SourceImageOffsetX));
            var destinationY = (int)Math.Min(normalized.Height, Math.Max(0L, background.SourceImageOffsetY));
            var copyWidth = Math.Min(source.Width - sourceX, normalized.Width - destinationX);
            var copyHeight = Math.Min(source.Height - sourceY, normalized.Height - destinationY);
            for (var y = 0; y < copyHeight; y++)
            {
                Buffer.BlockCopy(
                    source.Pixels,
                    ((sourceY + y) * source.Stride) + (sourceX * 4),
                    normalized.Pixels,
                    ((destinationY + y) * normalized.Stride) + (destinationX * 4),
                    copyWidth * 4);
            }

            var outputPath = ResolveOutputPath(backgroundPath);
            SavePng(normalized, outputPath);
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

    public static bool TryBakeLampOffArtwork(
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
            // MFME's Main image is both the graphical lamp's unlit/base artwork and
            // the image Oasis illuminates at runtime. Masks only select illuminated regions.
            // MapLampLike can emit several Oasis lamps sharing that Main image, so choose
            // exactly one representative for each original MFME component.
            var lamps = elements
                .Where(element => element.Kind == PanelElementKind.Lamp && !string.IsNullOrWhiteSpace(element.AssetPath))
                .Select((element, sequence) => (element, sequence))
                .GroupBy(item => item.element.SourceComponentIndex.HasValue
                    ? $"component:{item.element.SourceComponentIndex.Value}"
                    : !string.IsNullOrWhiteSpace(item.element.SharedSourceSetId)
                        ? $"set:{item.element.SharedSourceSetId}"
                        : $"element:{item.sequence}", StringComparer.Ordinal)
                .Select(group => group
                    .OrderBy(item => item.element.SourceElementIndex ?? int.MaxValue)
                    .ThenBy(item => item.sequence)
                    .First())
                .OrderBy(item => item.element.SourceComponentIndex ?? int.MaxValue)
                .ThenBy(item => item.sequence)
                .Select(item => item.element)
                .ToArray();

            if (lamps.Length == 0)
            {
                return true;
            }

            var backgroundImage = LoadBgra32(backgroundPath);
            foreach (var lamp in lamps)
            {
                if (TryResolveProjectAssetPath(lamp.AssetPath, projectAssetsRoot) is not { } lampPath || !File.Exists(lampPath))
                {
                    continue;
                }

                CompositeLampIntoBackground(backgroundImage, background, lamp, LoadBgra32(lampPath));
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

    private static void CompositeLampIntoBackground(PixelBuffer destination, PanelElementModel background, PanelElementModel lamp, PixelBuffer source)
    {
        var rect = GetElementDestinationRect(destination, background, lamp);
        for (var y = 0; y < rect.Height; y++)
        {
            var destinationY = rect.Y + y;
            if (destinationY < 0 || destinationY >= destination.Height) continue;
            var sourceY = ScaleCoordinate(y, rect.Height, source.Height);

            for (var x = 0; x < rect.Width; x++)
            {
                var destinationX = rect.X + x;
                if (destinationX < 0 || destinationX >= destination.Width) continue;
                var sourceX = ScaleCoordinate(x, rect.Width, source.Width);
                SourceOver(destination, destinationX, destinationY, source, sourceX, sourceY);
            }
        }
    }

    private static void SourceOver(PixelBuffer destination, int destinationX, int destinationY, PixelBuffer source, int sourceX, int sourceY)
    {
        var destinationOffset = (destinationY * destination.Stride) + (destinationX * 4);
        var sourceOffset = (sourceY * source.Stride) + (sourceX * 4);
        var sourceAlpha = source.Pixels[sourceOffset + 3];
        if (sourceAlpha == 0) return;
        if (sourceAlpha == 255)
        {
            Buffer.BlockCopy(source.Pixels, sourceOffset, destination.Pixels, destinationOffset, 4);
            return;
        }

        var destinationAlpha = destination.Pixels[destinationOffset + 3];
        var inverseSourceAlpha = 255 - sourceAlpha;
        var outputAlphaNumerator = (sourceAlpha * 255) + (destinationAlpha * inverseSourceAlpha);
        var outputAlpha = (outputAlphaNumerator + 127) / 255;
        for (var channel = 0; channel < 3; channel++)
        {
            var numerator = (source.Pixels[sourceOffset + channel] * sourceAlpha * 255)
                + (destination.Pixels[destinationOffset + channel] * destinationAlpha * inverseSourceAlpha);
            destination.Pixels[destinationOffset + channel] = (byte)((numerator + (outputAlphaNumerator / 2)) / outputAlphaNumerator);
        }
        destination.Pixels[destinationOffset + 3] = (byte)outputAlpha;
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

        var x = (int)Math.Round(overlayElement.X - background.X);
        var y = (int)Math.Round(overlayElement.Y - background.Y);
        var width = Math.Max(1, (int)Math.Round(overlayElement.Width));
        var height = Math.Max(1, (int)Math.Round(overlayElement.Height));
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
