using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using OasisEditor.FacePipeline;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class FaceArtworkRecipeEditorTests
{
    [Fact]
    public void RegistrationCanvasDragUsesTheFittedSourceImageNormalizedCoordinates()
    {
        HeadlessTestHost.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisArtworkEditor", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "source.png");
            WriteImage(source, 200, 100);
            var canvas = new FaceArtworkRecipeCanvas { Width = 400, Height = 300 };
            canvas.SetPreview(source);
            var window = new Window { Content = canvas, Width = 400, Height = 300 };
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            try
            {
                var map = ArtworkEditorImageMap.Fit(new Rect(canvas.Bounds.Size).Deflate(10), 200, 100);
                Assert.Null(ArtworkRegistrationEditInteraction.FindCorner(FacePerspectiveRegistrationModel.FullImage, new ArtworkEditorImageMap(map),
                    new Point(map.X + map.Width / 2, map.Y + map.Height / 2)));
                FacePerspectiveRegistrationModel? preview = null;
                canvas.RegistrationPreviewChanged += value => preview = value;
                var start = new Point(map.X + 2, map.Y + 2);
                var end = new Point(map.X + map.Width * .25, map.Y + map.Height * .3);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end);
                window.MouseUp(end, MouseButton.Left);

                Assert.NotNull(preview);
                Assert.Equal(.25, preview!.TopLeft.X, 6);
                Assert.Equal(.3, preview.TopLeft.Y, 6);
                Assert.Equal(1, preview.TopRight.X);
                Assert.Equal(1, preview.BottomRight.Y);
            }
            finally { window.Close(); canvas.ClearPreview(); Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void PanelInspectorPropertiesUpdateHistoryAndRejectEscapingAssetPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisPanelInspector", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var projectPath = Path.Combine(root, "Project.oasisproj");
            File.WriteAllText(projectPath, """{"version":9,"name":"Project","layout":{"assets":"Assets","generated":"Generated"}}""");
            var session = new AuthoringSession(root);
            session.OpenProject(projectPath);
            var panelPath = session.CreatePanel("Panel");
            var label = session.AddElement("label", "Text", 10, 20, 50, 20);
            session.UpdatePanelElementInspectorSettings(label, new PanelElementInspectorSettings(LampNumber: 4, DisplayText: "Play", TextColorHex: "#FFFFFFFF"));
            var initial = session.GetPanelElementInspectorSettings(label);

            Assert.True(session.UpdatePanelElementInspectorSettings(label, initial with { LampNumber = 8 }));
            Assert.False(session.UpdatePanelElementInspectorSettings(label, initial with { LampNumber = 8 }));
            Assert.Equal(8, session.GetPanelElementInspectorSettings(label).LampNumber);
            Assert.Equal("Play", session.GetPanelElementInspectorSettings(label).DisplayText);
            session.Undo();
            Assert.Equal(4, session.GetPanelElementInspectorSettings(label).LampNumber);
            session.Redo();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdatePanelElementInspectorSettings(label, initial with { LampNumber = -1 }));

            var image = Path.Combine(root, "image.png"); WriteImage(image, 200, 100);
            var background = session.ImportImage(image);
            Assert.Throws<InvalidDataException>(() => session.UpdatePanelElementInspectorSettings(background,
                new PanelElementInspectorSettings(AssetPath: "../../outside.png")));
            Assert.NotNull(session.GetPanelElementInspectorSettings(background).AssetPath);
            Assert.True(File.Exists(panelPath));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void WriteImage(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.SlateGray);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path); encoded.SaveTo(stream);
    }
}
