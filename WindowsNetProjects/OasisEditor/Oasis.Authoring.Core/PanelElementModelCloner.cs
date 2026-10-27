namespace OasisEditor;

internal static class PanelElementModelCloner
{
    public static PanelElementModel Clone(
        PanelElementModel source,
        string? objectId = null,
        string? name = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        string? assetPath = null,
        bool overrideAssetPath = false,
        string? secondaryAssetPath = null,
        bool overrideSecondaryAssetPath = false,
        string? sourceOffImageAssetPath = null,
        bool overrideSourceOffImageAssetPath = false,
        string? reelLampTransmissionMaskAssetPath = null,
        bool overrideReelLampTransmissionMaskAssetPath = false,
        bool? isTransformLocked = null,
        bool? isVisible = null,
        int? sourceImageOffsetX = null,
        int? sourceImageOffsetY = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PanelElementModel
        {
            ObjectId = objectId ?? source.ObjectId,
            Name = name ?? source.Name,
            Kind = source.Kind,
            X = x ?? source.X,
            Y = y ?? source.Y,
            Width = width ?? source.Width,
            Height = height ?? source.Height,
            AssetPath = overrideAssetPath ? assetPath : source.AssetPath,
            SecondaryAssetPath = overrideSecondaryAssetPath ? secondaryAssetPath : source.SecondaryAssetPath,
            SourceOffImageAssetPath = overrideSourceOffImageAssetPath ? sourceOffImageAssetPath : source.SourceOffImageAssetPath,
            DisplayNumber = source.DisplayNumber,
            LampNumber = source.LampNumber,
            SegmentDisplayType = source.SegmentDisplayType,
            DigitCount = source.DigitCount,
            ShowDecimalPoint = source.ShowDecimalPoint,
            ShowCommaTail = source.ShowCommaTail,
            HasBorder = source.HasBorder,
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
            BandOffset = source.BandOffset,
            ReelLampsEnabled = source.ReelLampsEnabled,
            ReelLamps = source.ReelLamps,
            IsOpaqueReel = source.IsOpaqueReel,
            ReelLampTransmissionMaskAssetPath = overrideReelLampTransmissionMaskAssetPath ? reelLampTransmissionMaskAssetPath : source.ReelLampTransmissionMaskAssetPath,
            IsTransformLocked = isTransformLocked ?? source.IsTransformLocked,
            IsVisible = isVisible ?? source.IsVisible,
            SourceComponentIndex = source.SourceComponentIndex,
            SourceElementIndex = source.SourceElementIndex,
            SharedSourceSetId = source.SharedSourceSetId,
            SharedSourceSetCount = source.SharedSourceSetCount,
            SourceBlend = source.SourceBlend,
            ImportSource = CloneImportSource(source.ImportSource),
            SourceImageOffsetX = sourceImageOffsetX ?? source.SourceImageOffsetX,
            SourceImageOffsetY = sourceImageOffsetY ?? source.SourceImageOffsetY
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
