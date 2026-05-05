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
    public Brush LitBrush { get; set; } = Brushes.OrangeRed;
    public Brush UnlitBrush { get; set; } = new SolidColorBrush(Color.FromArgb(120, 255, 69, 0));
    public string? DisplayText { get; set; }
    public bool ShowDecimalPoint { get; set; }

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
            var charIndex = i < (DisplayText?.Length ?? 0) ? i : -1;
            var segmentMask = charIndex >= 0 ? GetSegmentMaskForChar(DisplayText![charIndex]) : 0;
            var cellTransform = new MatrixTransform(scale, 0, 0, scale, offsetX + (i * pitch * scale), offsetY);

            foreach (var segment in _definition.Cell.Segments)
            {
                if (segment.Geometry is null)
                {
                    continue;
                }

                var lit = (segmentMask & (1 << segment.Index)) != 0;
                drawingContext.PushTransform(cellTransform);
                drawingContext.DrawGeometry(lit ? LitBrush : UnlitBrush, SegmentPen, segment.Geometry);
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
