using System.Windows;

namespace OasisEditor;

internal static class Panel2DMoveComputationService
{
    public static PanelElementModel ComputeMovedElement(
        PanelElementModel sourceElement,
        Point startDocumentPoint,
        Point endDocumentPoint)
    {
        ArgumentNullException.ThrowIfNull(sourceElement);

        var delta = endDocumentPoint - startDocumentPoint;
        return PanelElementModelCloner.Clone(
            sourceElement,
            x: sourceElement.X + delta.X,
            y: sourceElement.Y + delta.Y);
    }
}
