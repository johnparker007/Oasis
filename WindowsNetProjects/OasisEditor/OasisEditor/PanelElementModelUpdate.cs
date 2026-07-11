namespace OasisEditor;

internal readonly struct PanelElementOptionalValue<T>
{
    private readonly T _value;

    public PanelElementOptionalValue(T value)
    {
        HasValue = true;
        _value = value;
    }

    public bool HasValue { get; }

    public T Value => HasValue
        ? _value
        : throw new InvalidOperationException("No value has been assigned.");

    public static implicit operator PanelElementOptionalValue<T>(T value)
    {
        return new PanelElementOptionalValue<T>(value);
    }
}

internal sealed class PanelElementModelUpdate
{
    public PanelElementOptionalValue<string?> Name { get; init; }
    public PanelElementOptionalValue<double> X { get; init; }
    public PanelElementOptionalValue<double> Y { get; init; }
    public PanelElementOptionalValue<double> Width { get; init; }
    public PanelElementOptionalValue<double> Height { get; init; }
    public PanelElementOptionalValue<string?> AssetPath { get; init; }
    public PanelElementOptionalValue<string?> SecondaryAssetPath { get; init; }
    public PanelElementOptionalValue<int?> DisplayNumber { get; init; }
    public PanelElementOptionalValue<int?> LampNumber { get; init; }
    public PanelElementOptionalValue<string?> SegmentDisplayType { get; init; }
    public PanelElementOptionalValue<bool> ShowDecimalPoint { get; init; }
    public PanelElementOptionalValue<bool> ShowCommaTail { get; init; }
    public PanelElementOptionalValue<bool> HasBorder { get; init; }
    public PanelElementOptionalValue<string?> OnColorHex { get; init; }
    public PanelElementOptionalValue<string?> OffColorHex { get; init; }
    public PanelElementOptionalValue<string?> TextColorHex { get; init; }
    public PanelElementOptionalValue<string?> DisplayText { get; init; }
    public PanelElementOptionalValue<string?> TextBoxFontName { get; init; }
    public PanelElementOptionalValue<string?> TextBoxFontStyle { get; init; }
    public PanelElementOptionalValue<string?> TextBoxFontSize { get; init; }
    public PanelElementOptionalValue<bool?> IsReversed { get; init; }
    public PanelElementOptionalValue<int?> Stops { get; init; }
    public PanelElementOptionalValue<double?> VisibleScale { get; init; }
    public PanelElementOptionalValue<double?> BandOffset { get; init; }
    public PanelElementOptionalValue<bool> IsLocked { get; init; }
    public PanelElementOptionalValue<bool> IsVisible { get; init; }
}

internal static class PanelElementModelUpdater
{
    public static PanelElementModel Apply(PanelElementModel source, PanelElementModelUpdate update)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(update);

        return new PanelElementModel
        {
            ObjectId = source.ObjectId,
            Name = update.Name.HasValue ? update.Name.Value ?? string.Empty : source.Name,
            Kind = source.Kind,
            X = update.X.HasValue ? update.X.Value : source.X,
            Y = update.Y.HasValue ? update.Y.Value : source.Y,
            Width = update.Width.HasValue ? update.Width.Value : source.Width,
            Height = update.Height.HasValue ? update.Height.Value : source.Height,
            AssetPath = update.AssetPath.HasValue ? update.AssetPath.Value : source.AssetPath,
            SecondaryAssetPath = update.SecondaryAssetPath.HasValue ? update.SecondaryAssetPath.Value : source.SecondaryAssetPath,
            DisplayNumber = update.DisplayNumber.HasValue ? update.DisplayNumber.Value : source.DisplayNumber,
            LampNumber = update.LampNumber.HasValue ? update.LampNumber.Value : source.LampNumber,
            SegmentDisplayType = update.SegmentDisplayType.HasValue ? update.SegmentDisplayType.Value : source.SegmentDisplayType,
            ShowDecimalPoint = update.ShowDecimalPoint.HasValue ? update.ShowDecimalPoint.Value : source.ShowDecimalPoint,
            ShowCommaTail = update.ShowCommaTail.HasValue ? update.ShowCommaTail.Value : source.ShowCommaTail,
            HasBorder = update.HasBorder.HasValue ? update.HasBorder.Value : source.HasBorder,
            OnColorHex = update.OnColorHex.HasValue ? update.OnColorHex.Value : source.OnColorHex,
            OffColorHex = update.OffColorHex.HasValue ? update.OffColorHex.Value : source.OffColorHex,
            TextColorHex = update.TextColorHex.HasValue ? update.TextColorHex.Value : source.TextColorHex,
            DisplayText = update.DisplayText.HasValue ? update.DisplayText.Value : source.DisplayText,
            TextBoxFontName = update.TextBoxFontName.HasValue ? NormalizeLampFontName(update.TextBoxFontName.Value) : source.TextBoxFontName,
            TextBoxFontStyle = update.TextBoxFontStyle.HasValue ? NormalizeLampFontStyle(update.TextBoxFontStyle.Value) : source.TextBoxFontStyle,
            TextBoxFontSize = update.TextBoxFontSize.HasValue ? NormalizeLampFontSize(update.TextBoxFontSize.Value) : source.TextBoxFontSize,
            IsReversed = update.IsReversed.HasValue ? update.IsReversed.Value : source.IsReversed,
            Stops = update.Stops.HasValue ? update.Stops.Value : source.Stops,
            VisibleScale = update.VisibleScale.HasValue ? update.VisibleScale.Value : source.VisibleScale,
            BandOffset = update.BandOffset.HasValue ? update.BandOffset.Value : source.BandOffset,
            IsLocked = update.IsLocked.HasValue ? update.IsLocked.Value : source.IsLocked,
            IsVisible = update.IsVisible.HasValue ? update.IsVisible.Value : source.IsVisible,
            SourceComponentIndex = source.SourceComponentIndex,
            SourceElementIndex = source.SourceElementIndex,
            SharedSourceSetId = source.SharedSourceSetId,
            SharedSourceSetCount = source.SharedSourceSetCount,
            SourceBlend = source.SourceBlend,
            ImportSource = source.ImportSource is null
                ? null
                : new PanelElementImportSourceModel
                {
                    Format = source.ImportSource.Format,
                    Reference = source.ImportSource.Reference
                }
        };
    }

    private static string NormalizeLampFontName(string? value) => string.IsNullOrWhiteSpace(value) ? "Tahoma" : value.Trim();
    private static string NormalizeLampFontStyle(string? value) => string.IsNullOrWhiteSpace(value) ? "Regular" : value.Trim();
    private static string NormalizeLampFontSize(string? value) => string.IsNullOrWhiteSpace(value) ? "8" : value.Trim();
}
