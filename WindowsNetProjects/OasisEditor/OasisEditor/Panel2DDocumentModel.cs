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
}
