using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceRegisteredArtworkSourceTests
{
    [Fact]
    public void Registered_source_round_trips_semantic_normalized_corners_and_latest_schema()
    {
        var source = new FaceArtworkSourceModel
        {
            Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/FaceSources/photo.jpg",
            RegistrationQuad = new FaceArtworkRegistrationQuadModel
            {
                TopLeft = new() { X = .12, Y = .08 }, TopRight = new() { X = .91, Y = .11 },
                BottomRight = new() { X = .87, Y = .94 }, BottomLeft = new() { X = .09, Y = .9 }
            }
        };
        var json = FaceDocumentStorage.Serialize(new FaceDocumentModel { Artwork = new FaceArtworkModel { Source = source } });
        Assert.Contains($"\"SchemaVersion\": {FaceDocumentStorage.CurrentSchemaVersion}", json);
        Assert.True(FaceDocumentStorage.TryRead(json, out var file));
        var restored = FaceDocumentStorage.ToModel(file).Artwork!.Source;
        Assert.Equal(FaceArtworkSourceKind.RegisteredImage, restored.Kind);
        Assert.Equal(.12, restored.RegistrationQuad.TopLeft.X);
        Assert.Equal(.11, restored.RegistrationQuad.TopRight.Y);
        Assert.Equal(.87, restored.RegistrationQuad.BottomRight.X);
        Assert.Equal(.9, restored.RegistrationQuad.BottomLeft.Y);
    }

    [Fact]
    public void Full_image_quad_rectifies_without_distortion()
    {
        using var source = CreateCornerBitmap(41, 61);
        var quad = new[] { P(0, 0), P(40, 0), P(40, 60), P(0, 60) };
        using var result = PerspectiveRectificationService.Rectify(source, quad, 41, 61);
        Assert.Equal(source.GetPixel(0, 0), result.GetPixel(0, 0));
        Assert.Equal(source.GetPixel(40, 60), result.GetPixel(40, 60));
        Assert.Equal(source.GetPixel(20, 30), result.GetPixel(20, 30));
    }

    [Fact]
    public void Perspective_quad_maps_known_corner_and_centre_content()
    {
        using var source = CreateCornerBitmap(101, 101);
        var quad = new[] { P(10, 5), P(90, 10), P(80, 95), P(20, 90) };
        foreach (var (point, color) in new[] { (quad[0], SKColors.Red), (quad[1], SKColors.Green), (quad[2], SKColors.Blue), (quad[3], SKColors.Yellow) })
            source.SetPixel((int)point.X, (int)point.Y, color);
        using var result = PerspectiveRectificationService.Rectify(source, quad, 81, 86);
        Assert.Equal(SKColors.Red, result.GetPixel(0, 0));
        Assert.Equal(SKColors.Green, result.GetPixel(80, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(80, 85));
        Assert.Equal(SKColors.Yellow, result.GetPixel(0, 85));
    }

    [Fact]
    public void Registered_rebuild_preserves_source_resolution_and_writes_only_outputs_to_generated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-registered-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(root, "Assets", "FaceSources", "photo.png");
        var outputPath = Path.Combine(root, "Generated", "Faces", "Test", "Artwork", "artwork.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        using (var source = CreateCornerBitmap(1200, 1800)) Save(source, sourcePath);
        try
        {
            var artwork = new FaceArtworkModel { Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/FaceSources/photo.png" } };
            var result = new FaceArtworkRebuildService().RebuildRegisteredImage(artwork, root, outputPath, null, out var size);
            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(1200, size.Width); Assert.Equal(1800, size.Height);
            Assert.True(File.Exists(sourcePath)); Assert.True(File.Exists(outputPath));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(outputPath)!, "original.png")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Registration_normalization_clamps_points_without_reordering_semantics()
    {
        var normalized = new FaceArtworkRegistrationQuadModel
        {
            TopLeft = new() { X = 2, Y = -.5 }, TopRight = new() { X = -.2, Y = 2 },
            BottomRight = new() { X = .4, Y = .6 }, BottomLeft = new() { X = .7, Y = .8 }
        }.Normalize();
        Assert.Equal((1d, 0d), (normalized.TopLeft.X, normalized.TopLeft.Y));
        Assert.Equal((0d, 1d), (normalized.TopRight.X, normalized.TopRight.Y));
        Assert.Equal((.4, .6), (normalized.BottomRight.X, normalized.BottomRight.Y));
    }

    [Fact]
    public void Registration_edit_command_undoes_and_redoes_as_one_authored_change()
    {
        var model = new FaceDocumentModel { Artwork = new FaceArtworkModel { Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/a.png" } } };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(model));
        var edited = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.RegisteredImage, AssetPath = "Assets/a.png",
            RegistrationQuad = new FaceArtworkRegistrationQuadModel { TopLeft = new() { X = .2, Y = .3 }, TopRight = new() { X = 1 }, BottomRight = new() { X = 1, Y = 1 }, BottomLeft = new() { Y = 1 } } };
        var command = FaceMutationCommands.CreateSetArtworkSourceCommand(document.DocumentId, document, edited, "Move registration corner");
        command.Execute();
        Assert.Equal(.2, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
        command.Undo();
        Assert.Equal(0, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
        command.Execute();
        Assert.Equal(.2, document.GetFaceDocument().Artwork!.Source.RegistrationQuad.TopLeft.X);
    }

    private static FacePointModel P(double x, double y) => new() { X = x, Y = y };
    private static SKBitmap CreateCornerBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Red); bitmap.SetPixel(width - 1, 0, SKColors.Green);
        bitmap.SetPixel(width - 1, height - 1, SKColors.Blue); bitmap.SetPixel(0, height - 1, SKColors.Yellow);
        bitmap.SetPixel(width / 2, height / 2, SKColors.Magenta); return bitmap;
    }
    private static void Save(SKBitmap bitmap, string path)
    { using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = File.Create(path); data.SaveTo(stream); }
}
