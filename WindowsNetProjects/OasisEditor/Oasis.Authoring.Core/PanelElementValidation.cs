using OasisEditor;

namespace OasisEditor.FacePipeline;

internal static class PanelElementValidation
{
    public static bool IsFinite(double value) => double.IsFinite(value);
    public static bool IsPositiveFinite(double value) => IsFinite(value) && value > 0;
    public static bool IsValidBandOffset(double value) => IsFinite(value) && value >= -1d && value <= 1d;
    public static bool IsValidForInspectorUpdate(PanelElementModel element) =>
        IsFinite(element.X) && IsFinite(element.Y) && IsPositiveFinite(element.Width) && IsPositiveFinite(element.Height);
}
