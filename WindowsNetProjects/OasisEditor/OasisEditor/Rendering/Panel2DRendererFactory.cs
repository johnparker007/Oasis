namespace OasisEditor.Rendering;

internal static class Panel2DRendererFactory
{
    public static IPanel2DRenderer CreateDefault()
    {
        return new Panel2DRenderer(
        [
            new RectangleElementRenderer(),
            new ImageElementRenderer(),
            new BackgroundElementRenderer(),
            new LampElementRenderer(),
            new ReelElementRenderer(),
            new SevenSegmentElementRenderer(),
            new AlphaElementRenderer(),
            new LabelElementRenderer()
        ]);
    }
}
