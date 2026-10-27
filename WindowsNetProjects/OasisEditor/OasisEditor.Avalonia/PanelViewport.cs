using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OasisEditor.Avalonia;

/// <summary>Skia-backed Avalonia viewport. Drawing and hit testing use the same document transform.</summary>
public sealed class PanelViewport(AuthoringSession session) : Control, IDisposable
{
    private double _zoom = 1;
    private Vector _pan = new(30, 30);
    private bool _fit = true;
    private Point? _press;
    private Vector _originalPan;
    private ElementSnapshot? _drag;
    private Vector _preview;
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
        var elements = session.Elements;
        if (_fit && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var visible = elements.Where(e => e.Visible).ToArray();
            if (visible.Length > 0)
            {
                var x = visible.Min(e => e.X); var y = visible.Min(e => e.Y);
                var width = Math.Max(1, visible.Max(e => e.X + e.Width) - x);
                var height = Math.Max(1, visible.Max(e => e.Y + e.Height) - y);
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
            var offset = _drag?.Id == e.Id ? _preview : default;
            var rectangle = new Rect((e.X + offset.X) * _zoom + _pan.X, (e.Y + offset.Y) * _zoom + _pan.Y, e.Width * _zoom, e.Height * _zoom);
            if (!rectangle.Intersects(new Rect(Bounds.Size))) continue;
            var imageDrawn = false;
            if (!string.IsNullOrWhiteSpace(e.AssetPath))
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
                context.DrawRectangle(fill, new Pen(Brush("ElementOutline"), 1), rectangle);
                if (rectangle.Width > 30 && rectangle.Height > 18)
                {
                    var text = new FormattedText(e.DisplayText ?? e.Name, System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, Typeface.Default, 11, Color.TryParse(e.TextColorHex ?? "", out var tc) ? new SolidColorBrush(tc) : Brush("TextBrush"));
                    using (context.PushClip(rectangle)) context.DrawText(text, rectangle.TopLeft + new Vector(6, 4));
                }
            }
            if (e.Id == session.SelectedId)
            {
                var selection = Brush("SelectionColor");
                context.DrawRectangle(null, new Pen(selection, 2), rectangle);
                foreach (var corner in new[] { rectangle.TopLeft, rectangle.TopRight, rectangle.BottomLeft, rectangle.BottomRight })
                    context.FillRectangle(selection, new Rect(corner - new Vector(3, 3), new Size(6, 6)));
            }
        }
        if (elements.Count == 0)
        {
            var text = new FormattedText(session.PanelPath is null ? "Open a Panel2D asset to begin" : "Empty panel · Add a rectangle to begin",
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
            var hit = session.Elements.LastOrDefault(item => item.Visible && new Rect(item.X, item.Y, item.Width, item.Height).Contains(documentPoint));
            session.Select(hit?.Id);
            if (hit is { Locked: false }) { _press = position; _drag = hit; _preview = default; e.Pointer.Capture(this); }
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
        var drag = _drag; var offset = _preview;
        _press = null; _drag = null; _preview = default; e.Pointer.Capture(null);
        if (drag is not null && offset.Length > .01) session.UpdateElement(drag.Id, x: drag.X + offset.X, y: drag.Y + offset.Y);
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
