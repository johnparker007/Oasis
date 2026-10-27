using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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
    public IReadOnlyList<string> Warnings => _warnings;

    public void RequestFit() { _fit = true; InvalidateVisual(); }
    public void Fit() { _fit = true; InvalidateVisual(); }
    public void ClearImages() { foreach (var image in _images.Values) image.Dispose(); _images.Clear(); }

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
        foreach (var e in elements.Where(e => e.Visible))
        {
            var offset = !_resizing && _drag is not null && SelectedIds.Contains(e.Id) ? _preview : default;
            var size = _resizing && _drag?.Id == e.Id ? _preview : default;
            var rectangle = new Rect((e.X + offset.X) * _zoom + _pan.X, (e.Y + offset.Y) * _zoom + _pan.Y,
                Math.Max(1, e.Width + size.X) * _zoom, Math.Max(1, e.Height + size.Y) * _zoom);
            if (!rectangle.Intersects(new Rect(Bounds.Size))) continue;
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
            if (SelectedIds.Contains(e.Id))
            {
                var selection = Brush("SelectionColor");
                context.DrawRectangle(null, new Pen(selection, 2), rectangle);
                foreach (var corner in new[] { rectangle.TopLeft, rectangle.TopRight, rectangle.BottomLeft, rectangle.BottomRight })
                    context.FillRectangle(selection, new Rect(corner - new Vector(3, 3), new Size(6, 6)));
            }
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
