using System.Windows;
using System.Windows.Media;

namespace OasisEditor;

internal abstract class SegmentDisplayVisualBase : FrameworkElement
{
    private static readonly Pen SegmentPen = CreateSegmentPen();
    private readonly SegmentDisplayDefinition? _definition;

    protected SegmentDisplayVisualBase(SegmentDisplayDefinition? definition)
    {
        _definition = definition;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public int CellCount { get; set; } = 1;
    public Brush LitBrush { get; set; } = Brushes.Cyan;
    public Brush UnlitBrush { get; set; } = Brushes.DarkCyan;
    public string? DisplayText { get; set; }
    public int[]? CellSegmentMasks { get; set; }
    public bool ShowDecimalPoint { get; set; }
    public bool ShowCommaTail { get; set; }
    public double[]? CellBrightness { get; set; }
    public bool IsReversed { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_definition?.Cell?.Size is null || _definition.Cell.Segments is null)
        {
            return;
        }

        var cellSize = _definition.Cell.Size.AsSize;
        if (cellSize.Width <= 0 || cellSize.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var pitch = _definition.Cell.RecommendedPitch <= 0 ? cellSize.Width : _definition.Cell.RecommendedPitch;
        var sourceWidth = pitch * CellCount;
        var scale = Math.Min(ActualWidth / sourceWidth, ActualHeight / cellSize.Height);
        var offsetX = (ActualWidth - (sourceWidth * scale)) * 0.5;
        var offsetY = (ActualHeight - (cellSize.Height * scale)) * 0.5;

        for (var i = 0; i < CellCount; i++)
        {
            var dataIndex = IsReversed ? (CellCount - 1 - i) : i;
            var segmentMask = CellSegmentMasks is not null && dataIndex < CellSegmentMasks.Length
                ? CellSegmentMasks[dataIndex]
                : dataIndex < (DisplayText?.Length ?? 0)
                    ? GetSegmentMaskForChar(DisplayText![dataIndex])
                    : 0;
            var cellTransform = new MatrixTransform(scale, 0, 0, scale, offsetX + (i * pitch * scale), offsetY);
            var brightness = CellBrightness is not null && i < CellBrightness.Length ? Math.Clamp(CellBrightness[i], 0d, 1d) : 1d;

            foreach (var segment in _definition.Cell.Segments)
            {
                if (segment.Geometry is null)
                {
                    continue;
                }

                var bitIndex = segment.BitIndex ?? segment.Index;
                var lit = (segmentMask & (1 << bitIndex)) != 0;
                drawingContext.PushTransform(cellTransform);
                var brush = lit ? ResolveLitBrush(brightness) : UnlitBrush;
                drawingContext.DrawGeometry(brush, SegmentPen, segment.Geometry);
                drawingContext.Pop();
            }

            if (ShowDecimalPoint && _definition.Cell.DecimalPoint?.Geometry is not null)
            {
                var bitIndex = _definition.Cell.DecimalPoint.BitIndex ?? 16;
                var lit = (segmentMask & (1 << bitIndex)) != 0;
                drawingContext.PushTransform(cellTransform);
                drawingContext.DrawGeometry(lit ? ResolveLitBrush(brightness) : UnlitBrush, SegmentPen, _definition.Cell.DecimalPoint.Geometry);
                drawingContext.Pop();
            }

            if (ShowCommaTail && _definition.Cell.CommaTail?.Geometry is not null)
            {
                var bitIndex = _definition.Cell.CommaTail.BitIndex ?? 17;
                var lit = (segmentMask & (1 << bitIndex)) != 0;
                drawingContext.PushTransform(cellTransform);
                drawingContext.DrawGeometry(lit ? ResolveLitBrush(brightness) : UnlitBrush, SegmentPen, _definition.Cell.CommaTail.Geometry);
                drawingContext.Pop();
            }
        }
    }


    private Brush ResolveLitBrush(double brightness)
    {
        if (brightness >= 0.9999d)
        {
            return LitBrush;
        }

        if (brightness <= 0.0001d)
        {
            return UnlitBrush;
        }

        if (LitBrush is SolidColorBrush litSolid && UnlitBrush is SolidColorBrush unlitSolid)
        {
            var color = InterpolateColor(unlitSolid.Color, litSolid.Color, brightness);
            var interpolated = new SolidColorBrush(color);
            interpolated.Freeze();
            return interpolated;
        }

        return LitBrush;
    }

    private static Color InterpolateColor(Color from, Color to, double t)
    {
        byte lerp(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * t)), 0d, 255d);

        return Color.FromArgb(
            lerp(from.A, to.A),
            lerp(from.R, to.R),
            lerp(from.G, to.G),
            lerp(from.B, to.B));
    }

    protected abstract int GetSegmentMaskForChar(char c);

    private static Pen CreateSegmentPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 18, 18, 18)), 0.4);
        pen.Freeze();
        return pen;
    }
}
