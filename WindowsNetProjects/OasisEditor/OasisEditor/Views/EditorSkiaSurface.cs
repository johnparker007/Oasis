using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenTK.Wpf;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace OasisEditor.Views;

/// <summary>GPU rendering for connected desktop views; raster rendering for offscreen capture and fallback.</summary>
public sealed class EditorSkiaSurface : Grid
{
    private readonly SKElement _raster = new() { IsHitTestVisible = false };
    private GuardedGpuElement? _gpu;
    private bool _gpuFailed;

    public EditorSkiaSurface()
    {
        Background = Brushes.Transparent;
        Children.Add(_raster);
        _raster.PaintSurface += (_, args) => PaintSurface?.Invoke(this, args);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && IsLoaded) OnLoaded(this, new RoutedEventArgs());
            else if (!IsVisible) OnUnloaded(this, new RoutedEventArgs());
        };
    }

    public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;
    public bool IsGpuBacked => _gpu is not null;

    public new void InvalidateVisual()
    {
        base.InvalidateVisual();
        if (_gpu is not null) _gpu.InvalidateVisual();
        else _raster.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        // RenderTargetBitmap automation has no presentation source and must remain software-rendered.
        if (!IsVisible || _gpu is not null || _gpuFailed || PresentationSource.FromVisual(this) is null
            || string.Equals(Environment.GetEnvironmentVariable("OASIS_RENDER_BACKEND"), "cpu", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            _gpu = new GuardedGpuElement { IsHitTestVisible = false };
            _gpu.PaintSurface += OnGpuPaint;
            _gpu.Failed += OnGpuFailed;
            Children.Clear();
            Children.Add(_gpu);
        }
        catch (Exception exception) { FallBack(exception); }
    }

    private void OnGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, new SKPaintSurfaceEventArgs(args.Surface,
            new SKImageInfo(args.BackendRenderTarget.Width, args.BackendRenderTarget.Height)));

    private void OnGpuFailed(Exception exception)
        => Dispatcher.BeginInvoke(() => FallBack(exception));

    private void FallBack(Exception exception)
    {
        _gpuFailed = true;
        Trace.TraceWarning($"GPU viewport unavailable; using software rendering: {exception}");
        ReleaseGpu();
        if (!Children.Contains(_raster)) Children.Add(_raster);
        _raster.InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ReleaseGpu();
        if (!Children.Contains(_raster)) Children.Add(_raster);
    }

    private void ReleaseGpu()
    {
        var gpu = _gpu; _gpu = null;
        if (gpu is null) return;
        gpu.PaintSurface -= OnGpuPaint;
        gpu.Failed -= OnGpuFailed;
        Children.Remove(gpu);
        gpu.Dispose();
        // SKGLElement hides GLWpfControl.Dispose; both Skia and the WPF/GL bridge own native resources.
        ((GLWpfControl)gpu).Dispose();
    }

    private sealed class GuardedGpuElement : SKGLElement
    {
        private bool _failed;
        public event Action<Exception>? Failed;
        protected override void OnPaint(TimeSpan elapsed)
        {
            if (_failed || ActualWidth <= 0 || ActualHeight <= 0) return;
            try { base.OnPaint(elapsed); }
            catch (Exception exception) { _failed = true; Failed?.Invoke(exception); }
        }
    }
}
