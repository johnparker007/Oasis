namespace OasisEditor;

internal static class Panel2DSelectionNotificationService
{
    public static void NotifySelection(object viewOwner, DocumentTabViewModel document, PanelSelectionInfo? selection)
    {
        ArgumentNullException.ThrowIfNull(viewOwner);
        ArgumentNullException.ThrowIfNull(document);

        if (viewOwner is not System.Windows.DependencyObject dependencyObject)
        {
            document.HierarchySelectedPanelSelection = selection;
            return;
        }

        if (System.Windows.Window.GetWindow(dependencyObject)?.DataContext is MainWindowViewModel shellViewModel)
        {
            shellViewModel.UpdateDocumentPanelSelection(document.DocumentId, selection);
            return;
        }

        document.HierarchySelectedPanelSelection = selection;
    }
}
