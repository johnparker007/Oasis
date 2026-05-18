namespace OasisEditor.Rendering;

internal static class Panel2DRendererFactory
{
    public static IPanel2DRenderer CreateDefault()
    {
        return new Panel2DRenderer(
        [
            new BackgroundElementRenderer(),
            new LampElementRenderer(),
            new ReelElementRenderer(),
            new SevenSegmentElementRenderer(),
            new AlphaElementRenderer()
        ]);
    }
}
