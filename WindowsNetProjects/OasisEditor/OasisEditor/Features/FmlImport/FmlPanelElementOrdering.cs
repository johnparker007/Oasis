using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Features.FmlImport;

internal static class FmlPanelElementOrdering
{
    public static IReadOnlyList<PanelElementModel> ArrangeForBackgroundMode(IReadOnlyList<PanelElementModel> elements, FmlBackgroundMode mode)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return mode switch
        {
            FmlBackgroundMode.ImageBackedBackground => OrderForImageBackedBackground(elements),
            FmlBackgroundMode.SolidColourBackground => OrderForSolidColourBackground(elements),
            _ => OrderBySource(elements)
        };
    }

    private static IReadOnlyList<PanelElementModel> OrderForImageBackedBackground(IReadOnlyList<PanelElementModel> elements)
        => elements.OrderBy(element => IsDisplayCutoutElement(element) ? 0 : 1).ThenBy(SourceOrder).ThenBy(SourceElementOrder).ToArray();

    private static IReadOnlyList<PanelElementModel> OrderForSolidColourBackground(IReadOnlyList<PanelElementModel> elements)
        => elements.OrderBy(SolidColourLayer).ThenBy(SourceOrder).ThenBy(SourceElementOrder).ToArray();

    private static IReadOnlyList<PanelElementModel> OrderBySource(IReadOnlyList<PanelElementModel> elements)
        => elements.OrderBy(SourceOrder).ThenBy(SourceElementOrder).ToArray();

    private static int SolidColourLayer(PanelElementModel element)
        => element.Kind switch
        {
            PanelElementKind.Background when string.IsNullOrWhiteSpace(element.AssetPath) => 0,
            PanelElementKind.Reel => 1,
            PanelElementKind.SevenSegment => 2,
            PanelElementKind.Alpha or PanelElementKind.VfdDotMatrix => 3,
            PanelElementKind.Background when !string.IsNullOrWhiteSpace(element.AssetPath) => 4,
            PanelElementKind.Lamp => 5,
            PanelElementKind.Label => 7,
            _ => 8
        };

    private static bool IsDisplayCutoutElement(PanelElementModel element)
        => element.Kind is PanelElementKind.Reel or PanelElementKind.Alpha or PanelElementKind.SevenSegment or PanelElementKind.VfdDotMatrix;

    private static int SourceOrder(PanelElementModel element) => element.SourceComponentIndex ?? int.MaxValue;
    private static int SourceElementOrder(PanelElementModel element) => element.SourceElementIndex ?? int.MaxValue;
}
