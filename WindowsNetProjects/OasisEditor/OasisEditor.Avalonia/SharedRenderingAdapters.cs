using OasisEditor;
using SkiaSharp;

namespace OasisEditor;

/// <summary>Portable transform shape consumed by the existing Skia renderer sources.</summary>
public readonly record struct PanelViewportTransform(double Zoom, double PanX, double PanY,
    double ScaleY = double.NaN, double RasterMagnification = double.NaN, double ScaleX = double.NaN)
{
    public static PanelViewportTransform Identity => new(1, 0, 0);
    public double NormalizedZoom => double.IsFinite(ScaleX) && ScaleX > 0 ? ScaleX : Math.Clamp(Zoom, .01, 64);
    public double NormalizedScaleY => double.IsFinite(ScaleY) && ScaleY > 0 ? ScaleY : NormalizedZoom;
    public double NormalizedRasterMagnification => double.IsFinite(RasterMagnification) && RasterMagnification > 0 ? RasterMagnification : NormalizedZoom;
}

/// <summary>Only the renderer-facing policy from the WPF reel adapter is needed by the shared Skia pipeline.</summary>
internal static class MachineReelRuntimeAdapter
{
    internal static double ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType platform, int stops)
        => InternalReelOffsetResolver.ResolveNormalizedOffset(platform, stops);
}

/// <summary>Rendering needs only to know whether a native button has an authored machine input link.</summary>
public static class FaceInputTargetResolver
{
    public static bool TryGetInputReference(FaceButtonElement button, out MachineInputReference reference)
    {
        if (button.LinkedInputReference is MachineInputReference linked && !linked.Reference.IsEmpty && linked.Reference.Kind == MachineObjectKind.Input)
        { reference = linked; return true; }
        if (button.LinkedMachineObjectReference is MachineObjectReference machine && !machine.IsEmpty && machine.Kind == MachineObjectKind.Input)
        { reference = new MachineInputReference(machine); return true; }
        reference = default;
        return false;
    }
}

/// <summary>Crop mapping from the shared Face document model to its authored raster.</summary>
public static class FaceArtworkRasterMapping
{
    public static SKRect ResolveSourceRect(FaceArtworkElement element, int imageWidth, int imageHeight)
    {
        var region = element.SourceRegion;
        var source = element.Provenance?.SourceElementBounds;
        if (region is null || source is null || source.Width <= 0 || source.Height <= 0) return SKRect.Create(0, 0, imageWidth, imageHeight);
        var sx = imageWidth / source.Width; var sy = imageHeight / source.Height;
        var left = (float)Math.Clamp((region.X - source.X) * sx, 0, imageWidth);
        var top = (float)Math.Clamp((region.Y - source.Y) * sy, 0, imageHeight);
        var right = (float)Math.Clamp((region.X + region.Width - source.X) * sx, left, imageWidth);
        var bottom = (float)Math.Clamp((region.Y + region.Height - source.Y) * sy, top, imageHeight);
        return new SKRect(left, top, right, bottom);
    }
}

