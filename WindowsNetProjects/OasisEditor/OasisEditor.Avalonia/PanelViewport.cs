using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OasisEditor.Rendering;

namespace OasisEditor.Avalonia;

/// <summary>Skia-backed Avalonia viewport. Drawing and hit testing use the same document transform.</summary>
public sealed class PanelViewport(AuthoringSession session, FaceAuthoringSession? faces = null, Func<bool>? isFace = null) : Control, IDisposable
{
    private bool FaceActive => isFace?.Invoke() == true;
    private IReadOnlyList<ElementSnapshot> Elements => FaceActive ? faces!.Elements.Select(WorkspaceView.ToViewportElement).ToArray() : session.Elements;
    private IReadOnlyList<string> SelectedIds => FaceActive ? faces!.SelectedId is { } id ? [id] : [] : session.SelectedIds;
    public event Action<string>? CommandFailed;
    private double _zoom = 1;
    private Vector _pan = new(30, 30);
    private bool _fit = true;
    private Point? _press;
    private Vector _originalPan;
    private ElementSnapshot? _drag;
    private Vector _preview;
    private bool _resizing;
    private readonly Dictionary<string, Bitmap> _images = [];
    private readonly List<string> _warnings = [];
    private Func<RuntimeOutputSnapshot>? _runtimeOutputs;
    private static readonly IPanel2DRenderer PanelRenderer = Panel2DRendererFactory.Create("AvaloniaPanelViewport");
    private static readonly Face2DRenderer FaceRenderer = new();
    private Bitmap? _specialistBitmap;
    private string? _specialistBitmapKey;
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Connects a snapshot of real emulator outputs; authoring views use the empty authored state by default.</summary>
    public void SetRuntimeOutputProvider(Func<RuntimeOutputSnapshot>? provider) { _runtimeOutputs = provider; InvalidateVisual(); }
    /// <summary>Redraw after the connected runtime publishes a newer output snapshot.</summary>
    public void RefreshRuntimeOutputs() => InvalidateVisual();

    public void RequestFit() { _fit = true; InvalidateVisual(); }
    public void Fit() { _fit = true; InvalidateVisual(); }
    public WorkspaceViewportState CaptureViewState() => new(_zoom, _pan.X, _pan.Y, _fit);
    public void RestoreViewState(WorkspaceViewportState state)
    { state.Validate(); _zoom = state.Zoom; _pan = new Vector(state.PanX, state.PanY); _fit = state.Fit; InvalidateVisual(); }
    public void ClearImages()
    {
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear();
        _specialistBitmap?.Dispose(); _specialistBitmap = null; _specialistBitmapKey = null;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _warnings.Clear();
        var elements = Elements;
        if (_fit && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var visible = elements.Where(e => e.Visible).ToArray();
            if (visible.Length > 0 || FaceActive)
            {
                var x = FaceActive ? 0 : visible.Min(e => e.X); var y = FaceActive ? 0 : visible.Min(e => e.Y);
                var width = FaceActive ? Math.Max(1, faces!.LogicalWidth) : Math.Max(1, visible.Max(e => e.X + e.Width) - x);
                var height = FaceActive ? Math.Max(1, faces!.LogicalHeight) : Math.Max(1, visible.Max(e => e.Y + e.Height) - y);
                _zoom = Math.Clamp(Math.Min(Math.Max(1, Bounds.Width - 90) / width, Math.Max(1, Bounds.Height - 90) / height), .01, 16);
                _pan = new Vector((Bounds.Width - width * _zoom) / 2 - x * _zoom, (Bounds.Height - height * _zoom) / 2 - y * _zoom);
            }
        }
        var background = Brush("ViewportBackground"); var grid = new Pen(Brush("ViewportGrid"), 1);
        context.FillRectangle(background, new Rect(Bounds.Size));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        const double gridSize = 32;
        for (var x = _pan.X % gridSize; x < Bounds.Width; x += gridSize) context.DrawLine(grid, new Point(x, 0), new Point(x, Bounds.Height));
        for (var y = _pan.Y % gridSize; y < Bounds.Height; y += gridSize) context.DrawLine(grid, new Point(0, y), new Point(Bounds.Width, y));
        var specialistPipelineRendered = DrawSpecialistPipeline(context);
        foreach (var e in elements.Where(e => e.Visible))
        {
            var offset = !_resizing && _drag is not null && SelectedIds.Contains(e.Id) ? _preview : default;
            var size = _resizing && _drag?.Id == e.Id ? _preview : default;
            var rectangle = new Rect((e.X + offset.X) * _zoom + _pan.X, (e.Y + offset.Y) * _zoom + _pan.Y,
                Math.Max(1, e.Width + size.X) * _zoom, Math.Max(1, e.Height + size.Y) * _zoom);
            if (!rectangle.Intersects(new Rect(Bounds.Size))) continue;
            if (specialistPipelineRendered)
            {
                DrawSelection(context, e.Id, rectangle);
                continue;
            }
            var imageDrawn = false;
            if (!string.IsNullOrWhiteSpace(e.AssetPath) && (!FaceActive || e.Kind.Equals("artwork", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var path = session.ResolveAsset(e.AssetPath);
                    if (!_images.TryGetValue(path, out var image)) _images[path] = image = new Bitmap(path);
                    context.DrawImage(image, rectangle); imageDrawn = true;
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
                { _warnings.Add($"{e.Name}: {ex.Message}"); }
            }
            if (!imageDrawn)
            {
                var fill = Color.TryParse(e.OffColorHex ?? e.DisplayColorHex ?? "", out var color) ? new SolidColorBrush(color) : Brush("ElementFallback");
                context.DrawRectangle(FaceActive ? null : fill, new Pen(Brush("ElementOutline"), FaceActive ? .75 : 1), rectangle);
                if (rectangle.Width > 30 && rectangle.Height > 18 && (!FaceActive || SelectedIds.Contains(e.Id)))
                {
                    var text = new FormattedText(e.DisplayText ?? e.Name, System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, Typeface.Default, 11, Color.TryParse(e.TextColorHex ?? "", out var tc) ? new SolidColorBrush(tc) : Brush("TextBrush"));
                    using (context.PushClip(rectangle)) context.DrawText(text, rectangle.TopLeft + new Vector(6, 4));
                }
            }
            DrawSelection(context, e.Id, rectangle);
        }
        if (!FaceActive)
        {
            var regionPen = new Pen(Brush("SelectionColor"), 1.5, DashStyle.Dash);
            foreach (var shape in session.SourceShapes)
            {
                var points = shape.Points.Select(p => new Point(p.X * _zoom + _pan.X, p.Y * _zoom + _pan.Y)).ToArray();
                for (var i = 0; i < points.Length; i++) context.DrawLine(regionPen, points[i], points[(i + 1) % points.Length]);
                var label = new FormattedText(shape.Name, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 11, Brush("SelectionColor"));
                context.DrawText(label, points[0] + new Vector(4, -18));
            }
        }
        else
        {
            context.DrawRectangle(null, new Pen(Brush("ElementOutline"), 1),
                new Rect(_pan.X, _pan.Y, Math.Max(1, faces!.LogicalWidth) * _zoom, Math.Max(1, faces.LogicalHeight) * _zoom));
        }
        if (elements.Count == 0)
        {
            var text = new FormattedText(FaceActive ? "Empty Face · Add a lamp window to begin" : session.PanelPath is null ? "Open a Panel2D or Face asset to begin" : "Empty panel · Create an element to begin",
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 16, Brush("MutedBrush"));
            context.DrawText(text, new Point(Math.Max(20, (Bounds.Width - text.Width) / 2), Bounds.Height / 2));
        }
    }

    private bool DrawSpecialistPipeline(DrawingContext context)
    {
        var panelModel = FaceActive ? null : session.RenderDocument;
        var faceDocument = FaceActive ? faces?.Document : null;
        var panelElements = FaceActive ? [] : session.Elements.Where(item => item.Visible).ToArray();
        var documentWidth = FaceActive ? faces!.LogicalWidth : panelElements.Select(item => item.X + item.Width).DefaultIfEmpty(1).Max();
        var documentHeight = FaceActive ? faces!.LogicalHeight : panelElements.Select(item => item.Y + item.Height).DefaultIfEmpty(1).Max();
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || documentWidth <= 0 || documentHeight <= 0) return false;
        var outputs = _runtimeOutputs?.Invoke() ?? RuntimeOutputSnapshot.Empty;
        var documentRevision = FaceActive ? faces!.RenderRevision : session.RenderRevision;
        var projectDirectory = session.ProjectFilePath is { } project ? Path.GetDirectoryName(project) : null;
        ProjectAssetPathResolver.ProjectDirectoryPath = projectDirectory;
        var assetFingerprint = ComputeRendererAssetFingerprint(panelModel, faceDocument, projectDirectory);
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
        var width = Math.Clamp((int)Math.Ceiling(Bounds.Width * scale), 1, 8192);
        var height = Math.Clamp((int)Math.Ceiling(Bounds.Height * scale), 1, 8192);
        var activeDocumentPath = FaceActive ? faces?.FacePath : session.PanelPath;
        var cacheKey = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{(FaceActive ? 'F' : 'P')}|{activeDocumentPath}|{documentRevision}|{outputs.Fingerprint}|{assetFingerprint}|{_zoom:R}|{_pan.X:R}|{_pan.Y:R}|{width}|{height}|{scale:R}");
        if (_specialistBitmap is not null && string.Equals(cacheKey, _specialistBitmapKey, StringComparison.Ordinal))
        {
            context.DrawImage(_specialistBitmap, new Rect(Bounds.Size));
            return true;
        }
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null) return false;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Save();
        canvas.Scale((float)scale);
        canvas.Translate((float)_pan.X, (float)_pan.Y);
        canvas.Scale((float)_zoom);
        var transform = new PanelViewportTransform(_zoom, _pan.X, _pan.Y, _zoom, _zoom, _zoom);
        var runtime = CreateRuntimeState(outputs);
        try
        {
            if (FaceActive)
            {
                if (faceDocument is null) return false;
                FaceRenderer.Render(canvas, faceDocument, runtime, transform);
            }
            else if (panelModel is not null)
            {
                PanelRenderer.Render(canvas, panelModel.Elements, runtime, transform);
            }
            else return false;
            canvas.Restore();
            using var image = surface.Snapshot();
            using var rendered = SKBitmap.FromImage(image);
            if (rendered is null) return false;
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var framebuffer = bitmap.Lock())
            {
                var bytesPerRow = width * 4;
                var rowBuffer = new byte[bytesPerRow];
                for (var row = 0; row < height; row++)
                {
                    Marshal.Copy(IntPtr.Add(rendered.GetPixels(), row * rendered.RowBytes), rowBuffer, 0, bytesPerRow);
                    Marshal.Copy(rowBuffer, 0, IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), bytesPerRow);
                }
            }
            _specialistBitmap?.Dispose();
            _specialistBitmap = bitmap;
            _specialistBitmapKey = cacheKey;
            context.DrawImage(_specialistBitmap, new Rect(Bounds.Size));
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _warnings.Add($"Specialist rendering: {exception.Message}");
            return FaceActive ? faces?.Document is not null : session.RenderDocument is not null;
        }
    }

    private static string ComputeRendererAssetFingerprint(Panel2DDocumentModel? panel, FaceDocumentModel? face, string? projectDirectory)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path) { if (!string.IsNullOrWhiteSpace(path)) paths.Add(path.Trim()); }
        if (panel is not null)
        {
            foreach (var element in panel.Elements)
            {
                Add(element.AssetPath); Add(element.SecondaryAssetPath); Add(element.SourceOffImageAssetPath);
                Add(element.ReelLampTransmissionMaskAssetPath);
            }
        }
        if (face is not null)
        {
            foreach (var element in face.Elements)
            {
                switch (element)
                {
                    case FaceArtworkElement artwork: Add(artwork.AssetPath); break;
                    case FaceLampWindowElement lamp: Add(lamp.BulbMaskAssetPath); break;
                    case FaceReelMount reel: Add(reel.AssetPath); Add(reel.ReelLampTransmissionMaskAssetPath); break;
                }
            }
            Add(face.MaskLayer?.AssetPath); Add(face.MaskLayer?.AuthoredAssetPath);
            Add(face.Artwork?.CorrectionInputAssetPath); Add(face.Artwork?.BaseAssetPath); Add(face.Artwork?.OutputAssetPath);
            Add(face.Artwork?.Override?.AssetPath);
            var runtimeAssets = face.RuntimeRenderAssets;
            Add(runtimeAssets?.ArtworkPath); Add(runtimeAssets?.MaskPath); Add(runtimeAssets?.TrayIdPath);
            Add(runtimeAssets?.LampIds0Path); Add(runtimeAssets?.LampWeights0Path); Add(runtimeAssets?.LampIds1Path);
            Add(runtimeAssets?.LampWeights1Path); Add(runtimeAssets?.TrayIdDebugPath); Add(runtimeAssets?.LampWeightsDebugPath);
        }

        var builder = new StringBuilder();
        foreach (var asset in paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string resolved;
            try { resolved = Path.IsPathRooted(asset) ? Path.GetFullPath(asset) : projectDirectory is null ? asset : Path.GetFullPath(Path.Combine(projectDirectory, asset.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))); }
            catch (ArgumentException) { resolved = asset; }
            builder.Append(resolved).Append('|');
            try
            {
                var info = new FileInfo(resolved);
                builder.Append(info.Exists ? info.Length : -1).Append('|').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            { builder.Append("unavailable"); }
            builder.Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static MachineRuntimeState CreateRuntimeState(RuntimeOutputSnapshot outputs)
    {
        var state = new MachineRuntimeState { FruitMachinePlatform = outputs.Platform };
        foreach (var (id, value) in outputs.LampIntensities)
        {
            if (MachineObjectReference.TryParse(id, out var reference) && reference.Kind == MachineObjectKind.Lamp) state.SetLampIntensity(reference, value);
            else state.SetLampIntensity(id, value);
        }
        foreach (var (id, value) in outputs.ReelPositions)
        {
            if (MachineObjectReference.TryParse(id, out var reference) && reference.Kind == MachineObjectKind.Reel) state.SetReelPositionIfChanged(reference, value);
            else state.SetReelPositionIfChanged(id, value);
        }
        foreach (var (id, value) in outputs.TemporaryReelOffsets) state.SetTemporaryReelOffsetIfChanged(id, value);
        foreach (var (id, masks) in outputs.SegmentMasks)
        {
            if (MachineObjectReference.TryParse(id, out var reference)) state.SetSegmentCellMasksIfChanged(reference, masks.ToArray());
            else state.SetSegmentCellMasksIfChanged(id, masks.ToArray());
        }
        foreach (var (id, levels) in outputs.SegmentBrightness)
        {
            if (MachineObjectReference.TryParse(id, out var reference)) state.SetSegmentCellBrightnessIfChanged(reference, levels.ToArray());
            else state.SetSegmentCellBrightnessIfChanged(id, levels.ToArray());
        }
        foreach (var (id, dots) in outputs.VfdDots) state.SetVfdDotMatrixDotsIfChanged(id, dots.ToArray());
        return state;
    }

    private void DrawSelection(DrawingContext context, string id, Rect rectangle)
    {
        if (!SelectedIds.Contains(id)) return;
        var selection = Brush("SelectionColor");
        context.DrawRectangle(null, new Pen(selection, 2), rectangle);
        foreach (var corner in new[] { rectangle.TopLeft, rectangle.TopRight, rectangle.BottomLeft, rectangle.BottomRight })
            context.FillRectangle(selection, new Rect(corner - new Vector(3, 3), new Size(6, 6)));
    }

    private IBrush Brush(string key) => Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value switch
    {
        Color color => new SolidColorBrush(color), IBrush brush => brush,
        _ => throw new InvalidOperationException($"Theme resource '{key}' is not a brush or color.")
    } : throw new InvalidOperationException($"Theme resource '{key}' is missing.");

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var position = e.GetPosition(this); var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed) { _press = position; _originalPan = _pan; _drag = null; e.Pointer.Capture(this); e.Handled = true; }
        else if (properties.IsLeftButtonPressed)
        {
            var documentPoint = new Point((position.X - _pan.X) / _zoom, (position.Y - _pan.Y) / _zoom);
            var hit = Elements.LastOrDefault(item => item.Visible && new Rect(item.X, item.Y, item.Width, item.Height).Contains(documentPoint));
            var toggle = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Shift)) != 0;
            if (FaceActive) faces!.Select(hit?.Id);
            else if (toggle && hit is not null) session.SelectElements(SelectedIds.Contains(hit.Id) ? SelectedIds.Where(id => id != hit.Id).ToArray() : [.. SelectedIds, hit.Id]);
            else if (hit is null || !SelectedIds.Contains(hit.Id)) session.Select(hit?.Id);
            if (hit is { Locked: false } && SelectedIds.Contains(hit.Id))
            {
                _resizing = SelectedIds.Count == 1 && new Rect(new Point((hit.X + hit.Width) * _zoom + _pan.X - 7,
                    (hit.Y + hit.Height) * _zoom + _pan.Y - 7), new Size(14, 14)).Contains(position);
                _press = position; _drag = hit; _preview = default; e.Pointer.Capture(this);
            }
            InvalidateVisual(); e.Handled = true;
        }
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_press is not Point start) return;
        var delta = e.GetPosition(this) - start;
        if (_drag is null) { _pan = _originalPan + delta; _fit = false; }
        else { _fit = false; _preview = delta / _zoom; }
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var drag = _drag; var offset = _preview; var resizing = _resizing;
        _press = null; _drag = null; _preview = default; e.Pointer.Capture(null);
        try
        {
            if (drag is not null && offset.Length > .01)
            {
                if (resizing)
                {
                    if (FaceActive) faces!.UpdateElement(drag.Id, width: Math.Max(1, drag.Width + offset.X), height: Math.Max(1, drag.Height + offset.Y));
                    else session.UpdateElement(drag.Id, width: Math.Max(1, drag.Width + offset.X), height: Math.Max(1, drag.Height + offset.Y));
                }
                else if (FaceActive) faces!.UpdateElement(drag.Id, x: drag.X + offset.X, y: drag.Y + offset.Y);
                else session.MoveSelected(offset.X, offset.Y);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { CommandFailed?.Invoke(ex.Message); }
        InvalidateVisual();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { _press = null; _drag = null; _preview = default; InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var point = e.GetPosition(this); var next = Math.Clamp(_zoom * Math.Pow(1.12, e.Delta.Y), .01, 64);
        _pan = new Vector(point.X - (point.X - _pan.X) * next / _zoom, point.Y - (point.Y - _pan.Y) * next / _zoom);
        _zoom = next; _fit = false; InvalidateVisual(); e.Handled = true;
    }
    public void Dispose() => ClearImages();
}
