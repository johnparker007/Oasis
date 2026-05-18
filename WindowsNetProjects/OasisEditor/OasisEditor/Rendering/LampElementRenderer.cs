using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class LampElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.Lamp;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var intensity = context.RuntimeState.GetLampIntensity(element.ObjectId);
        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, SKColors.Red);
        var offColor = SkiaColorParser.ParseOrDefault(element.OffColorHex, new SKColor(40, 0, 0));
        var fill = Lerp(offColor, onColor, intensity);

        using var paint = new SKPaint
        {
            Color = fill,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        context.Canvas.DrawRoundRect(bounds, 4f, 4f, paint);
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);

        byte Blend(byte a, byte b)
        {
            return (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        }

        return new SKColor(
            Blend(from.Red, to.Red),
            Blend(from.Green, to.Green),
            Blend(from.Blue, to.Blue),
            Blend(from.Alpha, to.Alpha));
    }
}
