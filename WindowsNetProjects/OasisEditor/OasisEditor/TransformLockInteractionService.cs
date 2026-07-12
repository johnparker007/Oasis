namespace OasisEditor;

internal static class TransformLockInteractionService
{
    public static bool CanMoveOrResize(PanelElementModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return !element.IsTransformLocked;
    }

    public static bool CanMoveOrResize(FaceElementModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return !element.IsTransformLocked;
    }
}
