using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementFactoryTests
{
    [Theory]
    [InlineData((int)PanelElementKind.Background)]
    [InlineData((int)PanelElementKind.Lamp)]
    [InlineData((int)PanelElementKind.Reel)]
    [InlineData((int)PanelElementKind.SevenSegment)]
    [InlineData((int)PanelElementKind.Alpha)]
    public void CreateVisualFromElement_ForNativeKinds_PreservesElementKindForRoundTrip(int kindValue)
    {
        RunInSta(() =>
        {
            var kind = (PanelElementKind)kindValue;
            var source = new PanelElementFile
            {
                ObjectId = "obj-1",
                Name = "Element",
                Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
                X = 10,
                Y = 20,
                Width = 100,
                Height = 40
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            Assert.NotNull(visual);
            Assert.Equal(kind, PanelElementFactory.GetElementKind(visual!));

            var roundTrip = PanelElementFactory.CreateElementFromVisual(visual!);
            Assert.NotNull(roundTrip);
            Assert.Equal(kind, roundTrip!.ElementKind);
        });
    }

    [Fact]
    public void CreateVisualFromElement_LampWithoutImage_UsesLampNumberAndDisplayText()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "lamp-1",
                Name = "Lamp 9",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                Width = 90,
                Height = 40,
                DisplayNumber = 9,
                DisplayText = "HOLD",
                OnColorHex = "#FF00CC00"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);
            var detail = Assert.IsType<TextBlock>(stack.Children[1]);

            Assert.Equal("Lamp 9", title.Text);
            Assert.Equal("HOLD", detail.Text);
            Assert.Equal(Visibility.Visible, detail.Visibility);
        });
    }

    [Fact]
    public void CreateVisualFromElement_BackgroundWithoutImage_UsesConfiguredColor()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "bg-1",
                Name = "Background",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Background),
                Width = 320,
                Height = 180,
                OffColorHex = "#FF112233"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var brush = Assert.IsType<SolidColorBrush>(border.Background);
            Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), brush.Color);
        });
    }

    [Fact]
    public void CreateVisualFromElement_BackgroundWithProjectRelativeAsset_UsesImage()
    {
        RunInSta(() =>
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), $"oasis-panel-factory-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
            var imagePath = Path.Combine(projectRoot, "Assets", "bg.png");
            WriteTestPng(imagePath);

            var previousProjectDirectory = PanelElementFactory.ProjectDirectoryPath;

            try
            {
                PanelElementFactory.ProjectDirectoryPath = projectRoot;
                var source = new PanelElementFile
                {
                    ObjectId = "bg-2",
                    Name = "Background",
                    Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Background),
                    Width = 320,
                    Height = 180,
                    AssetPath = "Assets/bg.png"
                };

                var visual = PanelElementFactory.CreateVisualFromElement(source);
                var border = Assert.IsType<Border>(visual);

                Assert.IsType<Image>(border.Child);
            }
            finally
            {
                PanelElementFactory.ProjectDirectoryPath = previousProjectDirectory;
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, recursive: true);
                }
            }
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static void WriteTestPng(string path)
    {
        var pixels = new byte[]
        {
            0x00, 0x00, 0xFF, 0xFF,
            0x00, 0xFF, 0x00, 0xFF,
            0xFF, 0x00, 0x00, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF
        };

        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            8);

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }
}
