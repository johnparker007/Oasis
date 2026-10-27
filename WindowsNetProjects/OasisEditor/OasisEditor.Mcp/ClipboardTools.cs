using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "copy_selection"), Description("Copy the selected Panel2D elements, preserving full authored metadata. Uses a session-local clipboard and does not change document history.")]
    public Task<object> CopySelection(CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.CopySelectedToClipboard()), cancellationToken);

    [McpServerTool(Name = "cut_selection"), Description("Copy and delete the selected Panel2D elements. Deletion is one undoable document edit; clipboard contents remain available for paste.")]
    public Task<object> CutSelection(CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.CutSelectedToClipboard()), cancellationToken);

    [McpServerTool(Name = "paste_selection"), Description("Paste elements from this editor session's internal clipboard with fresh IDs and one undoable document edit. Asset references remain project-relative and are checked for path confinement.")]
    public Task<object> PasteSelection(CancellationToken cancellationToken, double offsetX = 16, double offsetY = 16)
        => ui.Invoke(() => Act(() => session.PasteClipboard(offsetX, offsetY)), cancellationToken);

    [McpServerTool(Name = "bring_selection_to_front"), Description("Move the selected Panel2D elements to the top of the stacking order, preserving relative order. One undoable edit.")]
    public Task<object> BringSelectionToFront(CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.BringSelectionToFront()), cancellationToken);

    [McpServerTool(Name = "send_selection_to_back"), Description("Move the selected Panel2D elements to the bottom of the stacking order, preserving relative order. One undoable edit.")]
    public Task<object> SendSelectionToBack(CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.SendSelectionToBack()), cancellationToken);
}
