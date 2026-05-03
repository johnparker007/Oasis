namespace OasisEditor;

internal static class PanelElementModelCloner
{
    public static PanelElementModel Clone(
        PanelElementModel source,
        string? objectId = null,
        string? name = null,
        double? x = null,
        double? y = null,
        bool? isLocked = null,
        bool? isVisible = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PanelElementModel
        {
            ObjectId = objectId ?? source.ObjectId,
            Name = name ?? source.Name,
            Kind = source.Kind,
            X = x ?? source.X,
            Y = y ?? source.Y,
            Width = source.Width,
            Height = source.Height,
            AssetPath = source.AssetPath,
            SecondaryAssetPath = source.SecondaryAssetPath,
            DisplayNumber = source.DisplayNumber,
            OnColorHex = source.OnColorHex,
            OffColorHex = source.OffColorHex,
            TextColorHex = source.TextColorHex,
            DisplayText = source.DisplayText,
            TextBoxFontName = source.TextBoxFontName,
            TextBoxFontStyle = source.TextBoxFontStyle,
            TextBoxFontSize = source.TextBoxFontSize,
            IsReversed = source.IsReversed,
            Stops = source.Stops,
            VisibleScale = source.VisibleScale,
            IsLocked = isLocked ?? source.IsLocked,
            IsVisible = isVisible ?? source.IsVisible,
            ImportSource = CloneImportSource(source.ImportSource)
        };
    }

    private static PanelElementImportSourceModel? CloneImportSource(PanelElementImportSourceModel? source)
    {
        if (source is null)
        {
            return null;
        }

        return new PanelElementImportSourceModel
        {
            Format = source.Format,
            Reference = source.Reference
        };
    }
}
