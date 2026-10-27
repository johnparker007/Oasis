using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class FaceTrayDebugOverlayRenderer
{
    public int Render(SKCanvas canvas, FaceDocumentModel faceDocument, PanelViewportTransform viewport)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(faceDocument);

        if (faceDocument.Trays.Count == 0 && faceDocument.LampEmitters.Count == 0)
        {
            return 0;
        }

        var drawCount = 0;
        using var autoTrayPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x00, 0xE5, 0xFF, 0xE8),
            StrokeWidth = (float)Math.Max(1d, 1.5d / viewport.NormalizedZoom),
            IsAntialias = true
        };
        using var manualTrayPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xC1, 0x07, 0xF0),
            StrokeWidth = (float)Math.Max(1d, 1.5d / viewport.NormalizedZoom),
            IsAntialias = true
        };
        using var labelPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0xF0),
            TextSize = (float)Math.Max(9d, 11d / Math.Sqrt(viewport.NormalizedZoom)),
            IsAntialias = true
        };
        using var labelShadowPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x00, 0x00, 0x00, 0xC8),
            TextSize = labelPaint.TextSize,
            IsAntialias = true
        };
        using var autoEmitterPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xFF, 0x40, 0x81, 0xF0),
            IsAntialias = true
        };
        using var manualEmitterPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xFF, 0xC1, 0x07, 0xF0),
            IsAntialias = true
        };
        using var emitterStrokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.White,
            StrokeWidth = (float)Math.Max(1d, 1d / viewport.NormalizedZoom),
            IsAntialias = true
        };

        foreach (var tray in faceDocument.Trays)
        {
            if (!TryGetTrayPath(tray, out var path, out var labelPoint))
            {
                continue;
            }

            using (path)
            {
                canvas.DrawPath(path, tray.IsAutoAuthored ? autoTrayPaint : manualTrayPaint);
            }

            DrawLabel(canvas, ResolveShortId(tray.ObjectId), labelPoint.X, labelPoint.Y - 3f, labelPaint, labelShadowPaint);
            drawCount++;
        }

        foreach (var emitter in faceDocument.LampEmitters)
        {
            if (!PanelElementValidation.IsFinite(emitter.CenterX) || !PanelElementValidation.IsFinite(emitter.CenterY))
            {
                continue;
            }

            var radius = (float)Math.Max(3d, 4d / viewport.NormalizedZoom);
            var x = (float)emitter.CenterX;
            var y = (float)emitter.CenterY;
            canvas.DrawCircle(x, y, radius, emitter.IsAutoAuthored ? autoEmitterPaint : manualEmitterPaint);
            canvas.DrawCircle(x, y, radius, emitterStrokePaint);
            var lampLabel = emitter.LampId is int lampId ? $"L{lampId}" : ResolveShortId(emitter.ObjectId);
            DrawLabel(canvas, lampLabel, x + radius + 2f, y - radius - 2f, labelPaint, labelShadowPaint);
            drawCount++;
        }

        return drawCount;
    }

    private static bool TryGetTrayPath(FaceTrayModel tray, out SKPath path, out SKPoint labelPoint)
    {
        path = new SKPath();
        labelPoint = default;

        var vertices = tray.Vertices
            .Where(vertex => PanelElementValidation.IsFinite(vertex.X) && PanelElementValidation.IsFinite(vertex.Y))
            .ToArray();
        if (vertices.Length >= 3)
        {
            path.MoveTo((float)vertices[0].X, (float)vertices[0].Y);
            foreach (var vertex in vertices.Skip(1))
            {
                path.LineTo((float)vertex.X, (float)vertex.Y);
            }

            path.Close();
            labelPoint = new SKPoint((float)vertices.Min(vertex => vertex.X), (float)vertices.Min(vertex => vertex.Y));
            return true;
        }

        if (tray.Bounds is not { IsValid: true } bounds)
        {
            path.Dispose();
            return false;
        }

        var rect = SKRect.Create((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        path.AddRect(rect);
        labelPoint = new SKPoint(rect.Left, rect.Top);
        return true;
    }

    private static void DrawLabel(SKCanvas canvas, string text, float x, float y, SKPaint labelPaint, SKPaint labelShadowPaint)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        canvas.DrawText(text, x + 1f, y + 1f, labelShadowPaint);
        canvas.DrawText(text, x, y, labelPaint);
    }

    private static string ResolveShortId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var trimmed = id.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[^8..];
    }
}
