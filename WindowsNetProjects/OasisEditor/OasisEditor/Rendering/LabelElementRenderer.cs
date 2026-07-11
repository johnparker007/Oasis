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

        var backgroundColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, SKColors.Transparent);
        if (backgroundColor.Alpha > 0)
        {
            using var backgroundPaint = new SKPaint { Color = backgroundColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            context.Canvas.DrawRect(bounds, backgroundPaint);
        }

        if (string.IsNullOrEmpty(element.DisplayText))
        {
            return;
        }

        var saveCount = context.Canvas.Save();
        try
        {
            context.Canvas.ClipRect(bounds, SKClipOperation.Intersect, antialias: true);
            using var textPaint = new SKPaint
            {
                Color = SkiaColorParser.ParseOrDefault(element.TextColorHex, SKColors.White),
                IsAntialias = true,
                TextSize = (float)TextElementRendererHelper.ParseFontSize(element.TextBoxFontSize),
                Typeface = TextElementRendererHelper.ResolveTypeface(element.TextBoxFontName, element.TextBoxFontStyle)
            };
            var textBounds = TextElementRendererHelper.GetInsetTextBounds(bounds);
            TextElementRendererHelper.DrawCenteredWrappedText(context.Canvas, element.DisplayText, bounds, textBounds, textPaint);
        }
        finally
        {
            context.Canvas.RestoreToCount(saveCount);
        }
    }
}
