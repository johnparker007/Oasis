using System.Windows;
using System.Windows.Media;

namespace OasisEditor;

internal sealed class AlphaSixteenSegmentDisplayVisual : FrameworkElement
{
    private readonly SegmentDisplayDefinition? _definition;

    public AlphaSixteenSegmentDisplayVisual()
    {
        SegmentDisplayDefinitionLoader.TryGetDefinition(out var definition);
        _definition = definition;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public int CellCount { get; set; } = 16;

    public Brush LitBrush { get; set; } = Brushes.OrangeRed;

    public Brush UnlitBrush { get; set; } = new SolidColorBrush(Color.FromArgb(120, 255, 69, 0));

    public string? DisplayText { get; set; }

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
            var segmentMask = charIndex >= 0 ? GetBasicMaskForChar(DisplayText![charIndex]) : 0;
            var cellTransform = new MatrixTransform(scale, 0, 0, scale, offsetX + (i * pitch * scale), offsetY);

            foreach (var segment in _definition.Cell.Segments)
            {
                if (segment.Geometry is null)
                {
                    continue;
                }

                var lit = (segmentMask & (1 << segment.Index)) != 0;
                drawingContext.PushTransform(cellTransform);
                drawingContext.DrawGeometry(lit ? LitBrush : UnlitBrush, null, segment.Geometry);
                drawingContext.Pop();
            }
        }
    }

    private static int GetBasicMaskForChar(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            >= '0' and <= '9' => 0b0000_1111_1111_1111,
            >= 'A' and <= 'Z' => 0b1111_1111_0000_1111,
            _ => 0
        };
    }
}
