using AvalonDock.Layout;
using System.Windows.Controls;

namespace OasisEditor.Views;

public partial class EditorShellView : UserControl
{
    public EditorShellView()
    {
        InitializeComponent();
    }

    public void ShowOrFocusToolWindow(EditorToolWindowId toolWindowId)
    {
        var target = FindToolWindow(toolWindowId);
        if (target is null)
        {
            return;
        }

        if (target.IsHidden)
        {
            target.Show();
            target.Float();
        }

        target.Show();
        target.IsSelected = true;
        target.IsActive = true;
    }

    private LayoutAnchorable? FindToolWindow(EditorToolWindowId toolWindowId)
    {
        var contentId = toolWindowId.ToString();
        return DockingManager.Layout.Descendents()
            .OfType<LayoutAnchorable>()
            .FirstOrDefault(anchorable => string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal));
    }
}
