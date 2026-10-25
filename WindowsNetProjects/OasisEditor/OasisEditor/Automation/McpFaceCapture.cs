using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OasisEditor.Views;

namespace OasisEditor.Automation;

internal static class McpFaceCapture
{
    public static byte[] Capture(DocumentTabViewModel document, string projectDirectory, int width, int height, ThemePreference theme)
    {
        if (width is < 320 or > 1920 || height is < 240 or > 1080) throw new ArgumentOutOfRangeException(nameof(width), "Capture size out of range.");
        if (Application.Current is null) throw new InvalidOperationException("Capture requires the MCP host's WPF dispatcher.");
        new ApplicationThemeService().ApplyTheme(Application.Current, theme);
        var previousProject = ProjectAssetPathResolver.ProjectDirectoryPath;
        var previousView = new EditorViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var view = new SkiaFaceEditView { DataContext = document, Width = width, Height = height };
        using var source = new HwndSource(new HwndSourceParameters("Oasis MCP offscreen Face capture", width, height)
        {
            WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000080,
            PositionX = -32000, PositionY = -32000
        });
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = projectDirectory;
            source.RootVisual = view;
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            view.FitToViewport(); view.RefreshForCapture();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }
        finally
        {
            try
            {
                source.RootVisual = null; view.DataContext = null;
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
            finally
            {
                try { document.FaceZoom = previousView.Zoom; document.FacePanX = previousView.PanX; document.FacePanY = previousView.PanY; }
                finally { ProjectAssetPathResolver.ProjectDirectoryPath = previousProject; }
            }
        }
    }
}
