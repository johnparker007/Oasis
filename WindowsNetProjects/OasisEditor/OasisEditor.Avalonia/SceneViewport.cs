using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OasisEditor.Avalonia.Rendering3D;
using Vector = Avalonia.Vector;

namespace OasisEditor.Avalonia;

public sealed record SceneHit(string Id, string Name, Vector3 Position, Vector2 TextureCoordinate, float Distance);

/// <summary>Interactive 3D viewport. The same offscreen GPU framebuffer is displayed and captured by MCP.</summary>
public sealed class SceneViewport : Control, IDisposable
{
    private GlSceneRenderer? _renderer;
    private SceneRenderData _scene = SceneRenderData.Empty;
    private SceneCameraState _camera = SceneCameraState.Default;
    private WriteableBitmap? _bitmap;
    private bool _invalid = true, _framed;
    private Point? _pointerStart, _previous;
    private bool _pan, _orbit;
    private bool _showVisuals = true, _showColliders, _showTriggers;
    private string? _failure;
    public event Action<SceneHit?>? Picked;
    public event Action<string>? Failed;
    public event Action? CameraChanged;
    public event Action? VisibilityChanged;
    public Func<SceneHit?, string?>? ResolvePreviewInput { get; set; }
    public event Action<string, bool>? PreviewInputRequested;
    private string? _previewInput;
    public SceneFrame? LastFrame { get; private set; }
    public string? RenderingError => _failure;
    public SceneRenderData Scene => _scene;
    public SceneCameraState Camera => _camera;
    public bool ShowVisuals { get => _showVisuals; set { if (_showVisuals == value) return; _showVisuals = value; InvalidateFrame(); VisibilityChanged?.Invoke(); } }
    public bool ShowColliders { get => _showColliders; set { if (_showColliders == value) return; _showColliders = value; InvalidateFrame(); VisibilityChanged?.Invoke(); } }
    public bool ShowTriggers { get => _showTriggers; set { if (_showTriggers == value) return; _showTriggers = value; InvalidateFrame(); VisibilityChanged?.Invoke(); } }
    public SceneViewport()
    {
        Focusable = true; ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => InvalidateFrame();
    }
    public void SetScene(SceneRenderData scene, bool frame = false)
    {
        _scene = scene; _failure = null;
        if (scene.Meshes.Count == 0) { _bitmap?.Dispose(); _bitmap = null; LastFrame = null; ReleaseRenderer(); }
        if (frame || !_framed) FrameAll();
        InvalidateFrame();
    }
    public void FrameAll()
    {
        _camera = SceneCameraState.Frame(_scene.Minimum, _scene.Maximum); _framed = true;
        InvalidateFrame(); CameraChanged?.Invoke();
    }
    public void SetCamera(SceneCameraState camera)
    { camera.Validate(); _camera = camera; _framed = true; InvalidateFrame(); CameraChanged?.Invoke(); }
    public void InvalidateFrame() { _invalid = true; LastFrame = null; InvalidateVisual(); }
    public void SuspendRendering() { _bitmap?.Dispose(); _bitmap = null; LastFrame = null; ReleaseRenderer(); InvalidateFrame(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == BoundsProperty) InvalidateFrame(); }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var background = ColorResource("ViewportBackground");
        context.FillRectangle(new SolidColorBrush(background), Bounds.WithX(0).WithY(0));
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (_scene.Meshes.Count > 0 && _failure is null && _invalid)
        {
            try
            {
                _renderer ??= new GlSceneRenderer();
                var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
                var width = Math.Clamp((int)Math.Ceiling(Bounds.Width * scale), 1, 4096);
                var height = Math.Clamp((int)Math.Ceiling(Bounds.Height * scale), 1, 4096);
                var frame = _renderer.Render(_scene, _camera, width, height,
                    new Vector4(background.R / 255f, background.G / 255f, background.B / 255f, 1), ShowVisuals, ShowColliders, ShowTriggers);
                var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale), PixelFormat.Bgra8888, AlphaFormat.Premul);
                try
                {
                    using var locked = bitmap.Lock();
                    for (var row = 0; row < height; row++) Marshal.Copy(frame.BgraPixels, row * width * 4, locked.Address + row * locked.RowBytes, width * 4);
                }
                catch { bitmap.Dispose(); throw; }
                _bitmap?.Dispose(); _bitmap = bitmap; LastFrame = frame; _invalid = false;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { _failure = "3D rendering unavailable: " + error.Message + " Reload the scene to recreate its graphics context.";
                _bitmap?.Dispose(); _bitmap = null; LastFrame = null; ReleaseRenderer();
                var message = _failure; global::Avalonia.Threading.Dispatcher.UIThread.Post(() => Failed?.Invoke(message)); }
        }
        if (_bitmap is not null) context.DrawImage(_bitmap, new Rect(Bounds.Size));
        else
        {
            var text = new FormattedText(_failure ?? "Open a Cabinet3D, Prefab or Machine to view its 3D scene.",
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 13,
                new SolidColorBrush(ColorResource("MutedBrush"))) { MaxTextWidth = Math.Max(50, Bounds.Width - 40) };
            context.DrawText(text, new Point(20, Math.Max(20, Bounds.Height / 2 - text.Height / 2)));
        }
    }
    private Color ColorResource(string key)
    {
        return this.TryFindResource(key, ActualThemeVariant, out var value) ? value switch
        { ISolidColorBrush brush => brush.Color, Color color => color, _ => throw new InvalidOperationException($"Theme resource {key} is not a color.") }
        : throw new InvalidOperationException($"Theme resource {key} is missing.");
    }
    public SceneHit? Pick(float normalizedX, float normalizedY)
    {
        var ray = _camera.Ray(normalizedX, normalizedY, (float)Math.Max(.01, Bounds.Width / Math.Max(1, Bounds.Height)));
        SceneHit? closest = null;
        foreach (var mesh in _scene.Meshes)
        {
            if (mesh.Lines || mesh.Kind is "grid" or "selection"
                || mesh.Kind == "visual" && !ShowVisuals || mesh.Kind == "collider" && !ShowColliders || mesh.Kind == "trigger" && !ShowTriggers) continue;
            for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
            {
                var a = Vertex(mesh.Indices[index]); var b = Vertex(mesh.Indices[index + 1]); var c = Vertex(mesh.Indices[index + 2]);
                var edge1 = b - a; var edge2 = c - a; var cross = Vector3.Cross(ray.Direction, edge2);
                var determinant = Vector3.Dot(edge1, cross);
                var frontDeterminant = mesh.Transform.GetDeterminant() < 0 ? -determinant : determinant;
                if (Math.Abs(determinant) < 1e-9 || !mesh.Material.DoubleSided && frontDeterminant <= 0) continue;
                var inverse = 1 / determinant; var delta = ray.Origin - a; var u = inverse * Vector3.Dot(delta, cross);
                if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(delta, edge1); var v = inverse * Vector3.Dot(ray.Direction, q);
                if (v < 0 || u + v > 1) continue;
                var distance = inverse * Vector3.Dot(edge2, q);
                if (distance < 0 || closest is not null && distance >= closest.Distance) continue;
                var uvA = Uv(mesh.Indices[index]); var uvB = Uv(mesh.Indices[index + 1]); var uvC = Uv(mesh.Indices[index + 2]);
                closest = new(mesh.Id, mesh.Name, ray.Origin + distance * ray.Direction, uvA * (1 - u - v) + uvB * u + uvC * v, distance);
                Vector3 Vertex(int i) => Vector3.Transform(new Vector3(mesh.Vertices[i * 8], mesh.Vertices[i * 8 + 1], mesh.Vertices[i * 8 + 2]), mesh.Transform);
                Vector2 Uv(int i) => new(mesh.Vertices[i * 8 + 6], mesh.Vertices[i * 8 + 7]);
            }
        }
        return closest;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var properties = e.GetCurrentPoint(this).Properties;
        _pointerStart = _previous = e.GetPosition(this);
        _pan = properties.IsMiddleButtonPressed;
        _orbit = properties.IsRightButtonPressed || properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (properties.IsLeftButtonPressed && !_orbit && ResolvePreviewInput is not null)
        {
            var point = e.GetPosition(this);
            _previewInput = ResolvePreviewInput(Pick((float)(2 * point.X / Math.Max(1, Bounds.Width) - 1), (float)(1 - 2 * point.Y / Math.Max(1, Bounds.Height))));
            if (_previewInput is not null) PreviewInputRequested?.Invoke(_previewInput, true);
        }
        e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_previous is not { } previous) return;
        var point = e.GetPosition(this); var delta = point - previous; _previous = point;
        if (_pan) SetCamera(_camera.Pan((float)(-delta.X / Math.Max(1, Bounds.Height)), (float)(delta.Y / Math.Max(1, Bounds.Height))));
        else if (_orbit) SetCamera(_camera.Orbit((float)(-delta.X * .008), (float)(delta.Y * .008)));
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        var wasInput = _previewInput is not null;
        ReleasePreviewPointer();
        if (!wasInput && !_pan && !_orbit && _pointerStart is { } start && Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2) < 25)
            Picked?.Invoke(Pick((float)(2 * point.X / Math.Max(1, Bounds.Width) - 1), (float)(1 - 2 * point.Y / Math.Max(1, Bounds.Height))));
        _pointerStart = _previous = null; _pan = _orbit = false; e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { base.OnPointerCaptureLost(e); ReleasePreviewPointer(); _pointerStart = _previous = null; _pan = _orbit = false; }
    private void ReleasePreviewPointer()
    { if (_previewInput is { } id) { _previewInput = null; PreviewInputRequested?.Invoke(id, false); } }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    { base.OnPointerWheelChanged(e); SetCamera(_camera.Zoom((float)e.Delta.Y)); e.Handled = true; }
    protected override void OnKeyDown(KeyEventArgs e)
    { base.OnKeyDown(e); if (e.Key == Key.F) { FrameAll(); e.Handled = true; } }
    public void Dispose()
    { _bitmap?.Dispose(); _bitmap = null; ReleaseRenderer(); LastFrame = null; }
    private void ReleaseRenderer()
    {
        try { _renderer?.Dispose(); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var message="3D graphics cleanup: "+error.Message;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(()=>Failed?.Invoke(message));
        }
        finally { _renderer = null; }
    }
}
