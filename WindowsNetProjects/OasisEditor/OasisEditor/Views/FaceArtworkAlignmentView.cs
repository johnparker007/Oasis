using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OasisEditor.Rendering;
using SkiaSharp;

namespace OasisEditor.Views;

/// <summary>Full-resolution alignment preview; changes to alignment and opacity reuse the decoded source.</summary>
public sealed class FaceArtworkAlignmentView : Grid
{
    private readonly EditorSkiaSurface _surface = new();
    private FaceWorkspaceViewModel? _workspace;
    private SKImage? _baseImage, _overrideImage;
    private string? _imageKey;
    private int _generation;
    private double _zoom = 1;
    private Vector _pan;
    private Point? _panStart;
    private Vector _panOrigin;
    internal EditorSkiaSurface PreviewSurface => _surface;
    internal int LoadedOverrideWidth => _overrideImage?.Width ?? 0;

    public FaceArtworkAlignmentView()
    {
        ClipToBounds = true;
        SetResourceReference(BackgroundProperty, "PanelBackgroundBrush");
        ToolTip = "Mouse wheel to zoom; middle-drag to pan.";
        Children.Add(_surface);
        _surface.PaintSurface += (_, args) => Draw(args.Surface.Canvas, args.Info.Width, args.Info.Height);
        Loaded += (_, _) => Subscribe();
        DataContextChanged += (_, _) => { Unsubscribe(); if (IsLoaded) Subscribe(); };
        Unloaded += (_, _) => { Unsubscribe(); ReleaseImages(); };
        IsVisibleChanged += (_, _) => { if (IsVisible && IsLoaded) RefreshImages(); };
        MouseWheel += (_, e) =>
        {
            var point = e.GetPosition(this); var centre = new Point(ActualWidth / 2, ActualHeight / 2);
            var next = Math.Clamp(_zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .1, 64);
            var ratio = next / _zoom;
            _pan = new Vector(point.X - centre.X - (point.X - centre.X - _pan.X) * ratio,
                point.Y - centre.Y - (point.Y - centre.Y - _pan.Y) * ratio);
            _zoom = next; _surface.InvalidateVisual(); e.Handled = true;
        };
        MouseDown += (_, e) => { if (e.ChangedButton != MouseButton.Middle) return; _panStart = e.GetPosition(this); _panOrigin = _pan; CaptureMouse(); e.Handled = true; };
        MouseMove += (_, e) => { if (_panStart is not { } start) return; _pan = _panOrigin + (e.GetPosition(this) - start); _surface.InvalidateVisual(); };
        MouseUp += (_, e) => { if (e.ChangedButton != MouseButton.Middle) return; _panStart = null; ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => _panStart = null;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property == BackgroundProperty) _surface?.InvalidateVisual();
    }

    private void Subscribe()
    {
        _workspace = (DataContext as DocumentTabViewModel)?.FaceWorkspace;
        if (_workspace is null) return;
        _workspace.SetLiveOverridePreview(true);
        _workspace.PropertyChanged += Changed;
        RefreshImages();
    }
    private void Unsubscribe()
    {
        if (_workspace is not null)
        {
            _workspace.PropertyChanged -= Changed;
            _workspace.ReportLiveOverridePreviewLoading(false);
            _workspace.SetLiveOverridePreview(false);
        }
        _workspace = null; _generation++;
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FaceWorkspaceViewModel.ArtworkOverride) or nameof(FaceWorkspaceViewModel.ArtworkBaseAbsolutePath)
            or nameof(FaceWorkspaceViewModel.Destination)) RefreshImages();
        _surface.InvalidateVisual();
    }
    private async void RefreshImages()
    {
        if (!IsVisible || DataContext is not DocumentTabViewModel doc || _workspace is null) return;
        var artwork = doc.GetFaceDocument().Artwork;
        var basePath = doc.GetArtworkAssetAbsolutePath(artwork?.BaseAssetPath);
        var overridePath = doc.GetArtworkAssetAbsolutePath(artwork?.Override?.AssetPath);
        var key = $"{basePath}|{Stamp(basePath)}|{overridePath}|{artwork?.Override?.ContentRevision}|{Stamp(overridePath)}";
        if (key == _imageKey) return;
        _imageKey = key; var generation = ++_generation;
        _workspace.ReportLiveOverridePreviewLoading(true);
        SKImage? baseImage = null, overlay = null;
        try
        {
            (baseImage, overlay) = await Task.Run(() =>
            {
                var first = Load(basePath);
                try { return (first, Load(overridePath)); }
                catch { first?.Dispose(); throw; }
            });
            if (generation != _generation) { baseImage?.Dispose(); overlay?.Dispose(); return; }
            _baseImage?.Dispose(); _overrideImage?.Dispose(); _baseImage = baseImage; _overrideImage = overlay;
            _workspace?.ReportLiveOverridePreviewLoading(false);
            _zoom = 1; _pan = default; _surface.InvalidateVisual();
        }
        catch (Exception exception)
        {
            baseImage?.Dispose(); overlay?.Dispose();
            if (generation == _generation)
            {
                _workspace?.ReportLiveOverridePreviewLoading(false);
                _imageKey = null; ReleaseImages(); _surface.InvalidateVisual();
            }
            Trace.TraceWarning($"Alignment preview could not load artwork: {exception}");
        }
    }
    private static long Stamp(string? path) => path is not null && File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
    private static SKImage? Load(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        using var bitmap = SKBitmap.Decode(path);
        return bitmap is null ? null : SKImage.FromBitmap(bitmap);
    }
    private void ReleaseImages()
    {
        _generation++; _imageKey = null;
        _baseImage?.Dispose(); _baseImage = null; _overrideImage?.Dispose(); _overrideImage = null;
    }
    private void Draw(SKCanvas canvas, int width, int height)
    {
        var background = (TryFindResource("PanelBackgroundBrush") as SolidColorBrush)?.Color ?? Colors.Transparent;
        canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));
        if (_baseImage is null) return;
        var scale = Math.Min(width / (double)_baseImage.Width, height / (double)_baseImage.Height) * _zoom;
        var dpi = VisualTreeHelper.GetDpi(this);
        canvas.Save();
        canvas.Translate((float)((width - _baseImage.Width * scale) / 2 + _pan.X * dpi.DpiScaleX),
            (float)((height - _baseImage.Height * scale) / 2 + _pan.Y * dpi.DpiScaleY));
        canvas.Scale((float)scale);
        canvas.DrawImage(_baseImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (_overrideImage is not null && _workspace?.ArtworkOverride is { } value)
        {
            var destination = SKRect.Create((float)(value.X * _baseImage.Width), (float)(value.Y * _baseImage.Height),
                (float)(value.Width * _baseImage.Width), (float)(value.Height * _baseImage.Height));
            FaceArtworkAlignmentRenderer.Draw(canvas, _overrideImage, value, destination, _workspace.OverridePreviewOpacity);
            var accent = (TryFindResource("AccentBrush") as SolidColorBrush)?.Color ?? Colors.Transparent;
            using var paint = new SKPaint { Color = new SKColor(accent.R, accent.G, accent.B, accent.A), Style = SKPaintStyle.Stroke, StrokeWidth = (float)(2 / scale), IsAntialias = true };
            canvas.DrawRect(destination, paint);
        }
        canvas.Restore();
    }
}
