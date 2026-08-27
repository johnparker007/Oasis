namespace OasisEditor;

/// <summary>Reference-raster pixels represented by one authored logical unit.</summary>
public readonly record struct EditorViewportContentScale(double PixelsPerLogicalUnitX, double PixelsPerLogicalUnitY)
{
    public static EditorViewportContentScale Identity => new(1d, 1d);

    public double X => Valid(PixelsPerLogicalUnitX);
    public double Y => Valid(PixelsPerLogicalUnitY);

    public static EditorViewportContentScale FromMapping(
        double rasterWidth, double rasterHeight, double logicalWidth, double logicalHeight)
    {
        if (rasterWidth <= 0d || rasterHeight <= 0d || logicalWidth <= 0d || logicalHeight <= 0d)
            return Identity;

        return new EditorViewportContentScale(rasterWidth / logicalWidth, rasterHeight / logicalHeight);
    }

    private static double Valid(double value) => double.IsFinite(value) && value > 0d ? value : 1d;
}
