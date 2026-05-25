namespace OasisEditor;

internal static class PanelElementValidation
{
    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static bool IsPositiveFinite(double value)
    {
        return IsFinite(value) && value > 0;
    }

    public static bool IsValidForInspectorUpdate(PanelElementModel element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return IsFinite(element.X)
               && IsFinite(element.Y)
               && IsPositiveFinite(element.Width)
               && IsPositiveFinite(element.Height);
    }
}

internal static class PanelElementModelComparer
{
    public static bool AreEquivalent(PanelElementModel left, PanelElementModel right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(left.ObjectId, right.ObjectId, StringComparison.Ordinal)
               && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
               && left.Kind == right.Kind
               && left.X.Equals(right.X)
               && left.Y.Equals(right.Y)
               && left.Width.Equals(right.Width)
               && left.Height.Equals(right.Height)
               && string.Equals(left.AssetPath, right.AssetPath, StringComparison.Ordinal)
               && string.Equals(left.SecondaryAssetPath, right.SecondaryAssetPath, StringComparison.Ordinal)
               && left.DisplayNumber == right.DisplayNumber
               && string.Equals(left.SegmentDisplayType, right.SegmentDisplayType, StringComparison.Ordinal)
               && left.ShowDecimalPoint == right.ShowDecimalPoint
               && left.ShowCommaTail == right.ShowCommaTail
               && string.Equals(left.OnColorHex, right.OnColorHex, StringComparison.Ordinal)
               && string.Equals(left.OffColorHex, right.OffColorHex, StringComparison.Ordinal)
               && string.Equals(left.TextColorHex, right.TextColorHex, StringComparison.Ordinal)
               && string.Equals(left.DisplayText, right.DisplayText, StringComparison.Ordinal)
               && string.Equals(left.TextBoxFontName, right.TextBoxFontName, StringComparison.Ordinal)
               && string.Equals(left.TextBoxFontStyle, right.TextBoxFontStyle, StringComparison.Ordinal)
               && string.Equals(left.TextBoxFontSize, right.TextBoxFontSize, StringComparison.Ordinal)
               && left.IsReversed == right.IsReversed
               && left.Stops == right.Stops
               && left.VisibleScale == right.VisibleScale
               && left.BandOffset == right.BandOffset
               && left.IsLocked == right.IsLocked
               && left.IsVisible == right.IsVisible
               && AreEquivalent(left.ImportSource, right.ImportSource);
    }

    private static bool AreEquivalent(PanelElementImportSourceModel? left, PanelElementImportSourceModel? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(left.Format, right.Format, StringComparison.Ordinal)
               && string.Equals(left.Reference, right.Reference, StringComparison.Ordinal);
    }
}
