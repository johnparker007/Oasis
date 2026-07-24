using System.IO;
using OasisEditor.Features.LayoutImport;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ReelLampTransmissionMaskGeneratorTests
{
    [Fact]
    public void TryGenerate_IsDeterministic()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-reel-mask-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "reel.png");
            using (var bitmap = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul))
            {
                bitmap.Erase(SKColors.White);
                bitmap.SetPixel(1, 1, SKColors.Red);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(source);
                data.SaveTo(stream);
            }

            var first = Path.Combine(root, "first.png");
            var second = Path.Combine(root, "second.png");
            Assert.True(MfmeReelLampTransmissionMaskGenerator.TryGenerate(source, first, out var firstError), firstError);
            Assert.True(MfmeReelLampTransmissionMaskGenerator.TryGenerate(source, second, out var secondError), secondError);

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
