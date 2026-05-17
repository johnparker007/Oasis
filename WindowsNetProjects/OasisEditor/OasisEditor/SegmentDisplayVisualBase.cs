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
    public double[]? CellBrightness { get; set; }

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
            var segmentMask = CellSegmentMasks is not null && i < CellSegmentMasks.Length
                ? CellSegmentMasks[i]
                : i < (DisplayText?.Length ?? 0)
                    ? GetSegmentMaskForChar(DisplayText![i])
                    : 0;
            var cellTransform = new MatrixTransform(scale, 0, 0, scale, offsetX + (i * pitch * scale), offsetY);
            var brightness = CellBrightness is not null && i < CellBrightness.Length ? Math.Clamp(CellBrightness[i], 0d, 1d) : 1d;

            foreach (var segment in _definition.Cell.Segments)
            {
                if (segment.Geometry is null)
                {
                    continue;
                }

                var lit = (segmentMask & (1 << segment.Index)) != 0;
                drawingContext.PushTransform(cellTransform);
                if (lit)
                {
                    drawingContext.PushOpacity(brightness);
                }

                drawingContext.DrawGeometry(lit ? LitBrush : UnlitBrush, SegmentPen, segment.Geometry);

                if (lit)
                {
                    drawingContext.Pop();
                }

                drawingContext.Pop();
            }

            if (ShowDecimalPoint && _definition.Cell.DecimalPoint?.Geometry is not null)
            {
                drawingContext.PushTransform(cellTransform);
                drawingContext.DrawGeometry(UnlitBrush, SegmentPen, _definition.Cell.DecimalPoint.Geometry);
                drawingContext.Pop();
            }
        }
    }

    protected abstract int GetSegmentMaskForChar(char c);

    private static Pen CreateSegmentPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 18, 18, 18)), 0.4);
        pen.Freeze();
        return pen;
    }
}
