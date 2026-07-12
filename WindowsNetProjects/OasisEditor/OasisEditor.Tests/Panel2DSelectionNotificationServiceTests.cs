using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DSelectionNotificationServiceTests
{
    [Fact]
    public void NotifySelection_WhenOwnerIsNotDependencyObject_UpdatesDocumentSelection()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreatePanel2DStub("Panel"),
            panelLayoutJson: null);
        var selection = new PanelSelectionInfo("id", "lamp", 1, 2, 3, 4);

        Panel2DSelectionNotificationService.NotifySelection(new object(), document, selection);

        Assert.Equal(selection, document.HierarchySelectedPanelSelection);
    }

    [Fact]
    public void NotifySelection_RaisesDedicatedSelectionChangedNotification()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var selection = new PanelSelectionInfo("id", "lamp", 1, 2, 3, 4);
        var raised = false;
        document.SelectionChanged += (_, _) => raised = true;

        Panel2DSelectionNotificationService.NotifySelection(new object(), document, selection);

        Assert.True(raised);
    }
}
