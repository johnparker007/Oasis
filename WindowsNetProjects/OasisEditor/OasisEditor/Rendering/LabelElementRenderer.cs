using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class LabelElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.Label;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(element.DisplayText) ? "Label" : element.DisplayText!;
        using var textPaint = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.TextColorHex, SKColors.LightSteelBlue),
            IsAntialias = true,
            TextSize = (float)LampElementRenderer.ParseFontSize(element.TextBoxFontSize)
        };

        var fontMetrics = textPaint.FontMetrics;
        var lineHeight = Math.Max(1f, Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent) + Math.Abs(fontMetrics.Leading));
        var textBounds = LampElementRenderer.GetTextBounds(bounds);
        var lines = LampElementRenderer.WrapTextToPixelWidth(text, textBounds.Width, textPaint);
        if (lines.Count == 0)
        {
            return;
        }

        var totalHeight = lines.Count * lineHeight;
        var baselineOffset = Math.Abs(fontMetrics.Ascent) > 0f ? Math.Abs(fontMetrics.Ascent) : textPaint.TextSize;
        var startY = textBounds.Top + ((textBounds.Height - totalHeight) / 2f) + baselineOffset;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrEmpty(line.Text))
            {
                continue;
            }

            var x = textBounds.Left + ((textBounds.Width - line.Width) / 2f);
            var y = startY + (lineIndex * lineHeight);
            context.Canvas.DrawText(line.Text, x, y, textPaint);
        }
    }
}
