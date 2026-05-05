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
    public void CreateVisualFromElement_LampWithoutImage_WithDisplayText_UsesDisplayTextOnly()
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

            Assert.Equal("HOLD", title.Text);
            Assert.Equal(string.Empty, detail.Text);
            Assert.Equal(Visibility.Collapsed, detail.Visibility);
        });
    }

    [Fact]
    public void CreateVisualFromElement_LampWithoutImage_WithoutDisplayText_ShowsNoText()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "lamp-2",
                Name = "Lamp 9",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                Width = 90,
                Height = 40,
                DisplayNumber = 9,
                DisplayText = null,
                OnColorHex = "#FF00CC00"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);
            var detail = Assert.IsType<TextBlock>(stack.Children[1]);

            Assert.Equal(string.Empty, title.Text);
            Assert.Equal(string.Empty, detail.Text);
            Assert.Equal(Visibility.Collapsed, detail.Visibility);
        });
    }


    [Fact]
    public void CreateVisualFromElement_LampWithoutImage_WithLongSingleLineText_UsesWrapWithOverflow()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "lamp-wrap-1",
                Name = "Lamp",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                Width = 40,
                Height = 40,
                DisplayText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890",
                OnColorHex = "#FF00CC00"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);

            Assert.Equal(TextWrapping.WrapWithOverflow, title.TextWrapping);
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

    [Fact]
    public void CreateVisualFromElement_LampWithoutImage_UsesTextColorForLabel()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "lamp-1",
                Name = "Lamp",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Lamp),
                Width = 80,
                Height = 36,
                DisplayText = "HOLD",
                OnColorHex = "#FFAA3300",
                TextColorHex = "#FF00FF00"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);
            var foreground = Assert.IsType<SolidColorBrush>(title.Foreground);

            Assert.Equal("HOLD", title.Text);
            Assert.Equal(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00), foreground.Color);
        });
    }

    [Fact]
    public void CreateVisualFromElement_ReelWithoutImage_UsesFallbackLabel()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "reel-1",
                Name = "Reel 3",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Reel),
                Width = 80,
                Height = 120,
                DisplayNumber = 3
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var stack = Assert.IsType<StackPanel>(border.Child);
            var title = Assert.IsType<TextBlock>(stack.Children[0]);
            Assert.Equal("Reel 3", title.Text);
        });
    }

    [Fact]
    public void CreateVisualFromElement_SevenSegment_RendersSevenSegmentDisplay()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "seven-1",
                Name = "7 Segment 4",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.SevenSegment),
                Width = 90,
                Height = 40,
                DisplayNumber = 4,
                DisplayText = "2",
                OnColorHex = "#FFCC2200"
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var display = Assert.IsType<SevenSegmentDisplayVisual>(border.Child);
            Assert.Equal("2", display.DisplayText);
            Assert.True(display.ShowDecimalPoint);
        });
    }


    [Fact]
    public void CreateVisualFromElement_AlphaReversed_RendersSegmentDisplayWithoutLabel()
    {
        RunInSta(() =>
        {
            var source = new PanelElementFile
            {
                ObjectId = "alpha-1",
                Name = "Alpha",
                Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Alpha),
                Width = 120,
                Height = 36,
                IsReversed = true
            };

            var visual = PanelElementFactory.CreateVisualFromElement(source);

            var border = Assert.IsType<Border>(visual);
            var display = Assert.IsType<AlphaSixteenSegmentDisplayVisual>(border.Child);
            Assert.Equal(16, display.CellCount);
            Assert.True(display.ShowDecimalPoint);
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
