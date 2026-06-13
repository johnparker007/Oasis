namespace OasisEditor;

internal sealed class Panel2DDocumentModel
{
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<PanelElementModel> Elements { get; init; } = [];
}

internal sealed class PanelElementModel
{
    public string ObjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public PanelElementKind Kind { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string? AssetPath { get; init; }
    public string? SecondaryAssetPath { get; init; }
    public int? DisplayNumber { get; init; }
    public string? SegmentDisplayType { get; init; }
    public bool ShowDecimalPoint { get; init; }
    public bool ShowCommaTail { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public string? TextColorHex { get; init; }
    public string? DisplayText { get; init; }
    public string? TextBoxFontName { get; init; }
    public string? TextBoxFontStyle { get; init; }
    public string? TextBoxFontSize { get; init; }
    public bool? IsReversed { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public double? BandOffset { get; init; }
    public bool IsLocked { get; init; }
    public bool IsVisible { get; init; } = true;
    public int? SourceComponentIndex { get; init; }
    public int? SourceElementIndex { get; init; }
    public string? SharedSourceSetId { get; init; }
    public int? SharedSourceSetCount { get; init; }
    public PanelElementImportSourceModel? ImportSource { get; init; }
}

internal sealed class PanelElementImportSourceModel
{
    public string Format { get; init; } = string.Empty;
    public string? Reference { get; init; }
}
