namespace OasisEditor;

internal static class RuntimePanelElementValidation
{
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
