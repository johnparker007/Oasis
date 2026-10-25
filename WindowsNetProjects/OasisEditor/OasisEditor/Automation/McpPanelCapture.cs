using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Views;

namespace OasisEditor.Automation;

internal static class McpPanelCapture
{
    public static byte[] Capture(DocumentTabViewModel document, string projectDirectory, int width, int height, ThemePreference theme)
    {
        if (width is < 320 or > 1920 || height is < 240 or > 1080) throw new ArgumentOutOfRangeException(nameof(width), "Capture size out of range.");
        if (Application.Current is null) throw new InvalidOperationException("Capture requires the MCP host's WPF dispatcher.");
        new ApplicationThemeService().ApplyTheme(Application.Current, theme);
        var previousProject = ProjectAssetPathResolver.ProjectDirectoryPath;
        var view = new SkiaPanel2DEditView { DataContext = document, Width = width, Height = height };
        // Skia's WPF element needs a presentation source to initialize rendering.
        // An offscreen tool window supplies it without activating or entering the taskbar.
        using var source = new HwndSource(new HwndSourceParameters("Oasis MCP offscreen capture", width, height)
        {
            WindowStyle = unchecked((int)0x90000000), // WS_POPUP | WS_VISIBLE
            ExtendedWindowStyle = 0x08000080, // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            PositionX = -32000,
            PositionY = -32000
        });
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = projectDirectory;
            source.RootVisual = view;
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        finally
        {
            source.RootVisual = null;
            view.DataContext = null;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            ProjectAssetPathResolver.ProjectDirectoryPath = previousProject;
        }
    }
}
