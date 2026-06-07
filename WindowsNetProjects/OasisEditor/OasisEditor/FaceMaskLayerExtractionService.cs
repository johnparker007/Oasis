using System.IO;
using SkiaSharp;
using System.Windows;

namespace OasisEditor;

internal sealed class FaceMaskLayerExtractionService
{
    public const byte DefaultExtractionThreshold = 24;

    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;

    public FaceMaskLayerExtractionService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
    {
        _machineObjectReferenceResolver = machineObjectReferenceResolver ?? MachineObjectReferenceResolver.Instance;
    }

    public FaceMaskLayerModel? GenerateMaskLayer(
        Panel2DDocumentModel sourcePanel,
        Rect region,
        string faceDocumentId,
        string? sourcePanel2DDocumentId,
        string? projectDirectory,
        string? generatedDirectory,
        byte extractionThreshold = DefaultExtractionThreshold)
    {
        ArgumentNullException.ThrowIfNull(sourcePanel);

        if (region.Width <= 0 || region.Height <= 0)
        {
            return null;
        }

        var width = Math.Max(1, (int)Math.Ceiling(region.Width));
        var height = Math.Max(1, (int)Math.Ceiling(region.Height));
        var maskPixels = new byte[width * height];
        var contributions = new List<FaceMaskContributionModel>();
        var backgrounds = sourcePanel.Elements
            .Where(element => element.Kind == PanelElementKind.Background && Intersects(element, region))
            .Select(element => new SourceImageElement(element, TryLoadBitmap(element.AssetPath, projectDirectory)))
            .Where(source => source.Bitmap is not null)
            .ToArray();

        foreach (var lamp in sourcePanel.Elements.Where(element => element.Kind == PanelElementKind.Lamp && IsContainedBy(element, region)))
        {
            using var lampBitmap = TryLoadBitmap(lamp.AssetPath, projectDirectory);
            if (lampBitmap is null)
            {
                continue;
            }

            var contribution = CompositeLampContribution(maskPixels, width, height, region, backgrounds, lamp, lampBitmap, extractionThreshold);
            if (contribution.PixelCount <= 0 || contribution.Bounds is null)
            {
                continue;
            }

            _machineObjectReferenceResolver.TryGetReference(lamp, out var machineReference);
            contributions.Add(new FaceMaskContributionModel
            {
                SourcePanel2DElementId = string.IsNullOrWhiteSpace(lamp.ObjectId) ? null : lamp.ObjectId,
                LinkedMachineObjectReference = machineReference.IsEmpty ? null : machineReference,
                Bounds = contribution.Bounds,
                PixelCount = contribution.PixelCount
            });
        }

        foreach (var background in backgrounds)
        {
            background.Bitmap?.Dispose();
        }

        var assetPath = SaveMask(maskPixels, width, height, faceDocumentId, projectDirectory, generatedDirectory);
        return new FaceMaskLayerModel
        {
            Id = "face-mask-layer",
            Name = "Face Mask",
            AssetPath = assetPath,
            SourcePanel2DDocumentId = string.IsNullOrWhiteSpace(sourcePanel2DDocumentId) ? null : sourcePanel2DDocumentId.Trim(),
            SourceRegion = FaceSourceRegionModel.FromRect(region),
            ExtractionThreshold = extractionThreshold,
            GeneratedUtc = DateTime.UtcNow,
            Width = width,
            Height = height,
            Contributions = contributions.ToArray()
        };
    }

    private static ContributionResult CompositeLampContribution(
        byte[] maskPixels,
        int width,
        int height,
        Rect region,
        IReadOnlyList<SourceImageElement> backgrounds,
        PanelElementModel lamp,
        SKBitmap lampBitmap,
        byte threshold)
    {
        var left = Math.Max(0, (int)Math.Floor(lamp.X - region.X));
        var top = Math.Max(0, (int)Math.Floor(lamp.Y - region.Y));
        var right = Math.Min(width, (int)Math.Ceiling(lamp.X + lamp.Width - region.X));
        var bottom = Math.Min(height, (int)Math.Ceiling(lamp.Y + lamp.Height - region.Y));
        var count = 0;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var documentX = region.X + x + 0.5;
                var documentY = region.Y + y + 0.5;
                var on = Sample(lampBitmap, lamp, documentX, documentY);
                if (on.Alpha == 0)
                {
                    continue;
                }

                var background = SampleBackground(backgrounds, documentX, documentY);
                var delta = Brightness(on) - Brightness(background);
                if (delta < threshold)
                {
                    continue;
                }

                maskPixels[(y * width) + x] = 255;
                count++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        var bounds = maxX < minX || maxY < minY
            ? null
            : FaceSourceRegionModel.FromRect(new Rect(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1));
        return new ContributionResult(bounds, count);
    }

    private static SKColor SampleBackground(IReadOnlyList<SourceImageElement> backgrounds, double documentX, double documentY)
    {
        for (var i = backgrounds.Count - 1; i >= 0; i--)
        {
            var source = backgrounds[i];
            if (source.Bitmap is not null && Contains(source.Element, documentX, documentY))
            {
                return Sample(source.Bitmap, source.Element, documentX, documentY);
            }
        }

        return SKColors.Black;
    }

    private static SKColor Sample(SKBitmap bitmap, PanelElementModel element, double documentX, double documentY)
    {
        if (element.Width <= 0 || element.Height <= 0 || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return SKColors.Black;
        }

        var u = Math.Clamp((documentX - element.X) / element.Width, 0d, 0.999999d);
        var v = Math.Clamp((documentY - element.Y) / element.Height, 0d, 0.999999d);
        var x = Math.Clamp((int)Math.Floor(u * bitmap.Width), 0, bitmap.Width - 1);
        var y = Math.Clamp((int)Math.Floor(v * bitmap.Height), 0, bitmap.Height - 1);
        return bitmap.GetPixel(x, y);
    }

    private static double Brightness(SKColor color)
    {
        return ((0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue)) * (color.Alpha / 255d);
    }

    private static SKBitmap? TryLoadBitmap(string? assetPath, string? projectDirectory)
    {
        if (!TryResolveAssetPath(assetPath, projectDirectory, out var resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        using var codec = SKCodec.Create(resolvedPath);
        return codec is null ? null : SKBitmap.Decode(codec);
    }

    private static string? SaveMask(byte[] maskPixels, int width, int height, string faceDocumentId, string? projectDirectory, string? generatedDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || string.IsNullOrWhiteSpace(generatedDirectory))
        {
            return null;
        }

        var facesDirectory = Path.Combine(generatedDirectory, "Faces");
        Directory.CreateDirectory(facesDirectory);
        var safeId = string.IsNullOrWhiteSpace(faceDocumentId) ? Guid.NewGuid().ToString("N") : SanitizeFileName(faceDocumentId);
        var fullPath = Path.Combine(facesDirectory, $"{safeId}-mask.png");

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = maskPixels[(y * width) + x];
                bitmap.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);

        return Path.GetRelativePath(projectDirectory, fullPath).Replace('\\', '/');
    }

    private static bool TryResolveAssetPath(string? assetPath, string? projectDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return false;
        }

        resolvedPath = Path.GetFullPath(Path.Combine(projectDirectory, candidate.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        return true;
    }

    private static bool Contains(PanelElementModel element, double documentX, double documentY)
    {
        return documentX >= element.X
            && documentY >= element.Y
            && documentX < element.X + element.Width
            && documentY < element.Y + element.Height;
    }

    private static bool IsContainedBy(PanelElementModel element, Rect region)
    {
        var left = element.X;
        var top = element.Y;
        var right = element.X + element.Width;
        var bottom = element.Y + element.Height;
        return left >= region.Left
            && top >= region.Top
            && right <= region.Right
            && bottom <= region.Bottom;
    }

    private static bool Intersects(PanelElementModel element, Rect region)
    {
        return element.Width > 0
            && element.Height > 0
            && new Rect(element.X, element.Y, element.Width, element.Height).IntersectsWith(region);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private sealed record SourceImageElement(PanelElementModel Element, SKBitmap? Bitmap);
    private sealed record ContributionResult(FaceSourceRegionModel? Bounds, int PixelCount);
}
