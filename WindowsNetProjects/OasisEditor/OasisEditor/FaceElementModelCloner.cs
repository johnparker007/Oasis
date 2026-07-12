namespace OasisEditor;

internal static class FaceElementModelCloner
{
    public static FaceElementModel Clone(FaceElementModel source, double? x = null, double? y = null, double? width = null, double? height = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return FaceElementModelUpdater.Apply(source, new FaceElementModelUpdate
        {
            X = x ?? source.X,
            Y = y ?? source.Y,
            Width = width ?? source.Width,
            Height = height ?? source.Height
        });
    }
}
