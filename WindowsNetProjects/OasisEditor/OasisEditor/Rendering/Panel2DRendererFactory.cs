namespace OasisEditor.Rendering;

internal static class Panel2DRendererFactory
{
    public static IPanel2DRenderer Create(string viewName)
        => new Panel2DRenderer(CreateDefaultRenderers(), viewName);

    public static IReadOnlyList<IPanelElementRenderer> CreateDefaultRenderers() =>
    [
        new BackgroundElementRenderer(),
        new ImageElementRenderer(),
        new LampElementRenderer(),
        new ReelElementRenderer(),
        new SevenSegmentElementRenderer(),
        new AlphaElementRenderer(),
        new VfdDotMatrixElementRenderer(),
        new LabelElementRenderer()
    ];
}
