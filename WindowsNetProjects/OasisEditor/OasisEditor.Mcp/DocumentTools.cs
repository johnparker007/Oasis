using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name="list_open_documents",ReadOnly=true),Description("Inspect retained asset tabs, scope, dirty state and active tab. Unsaved contents stay in document-local history.")]
    public Task<IReadOnlyList<WorkspaceDocumentTab>> ListOpenDocuments(CancellationToken cancellationToken)
        =>ui.Invoke(capture.View.InspectOpenDocuments,cancellationToken);

    [McpServerTool(Name="activate_document"),Description("Activate an already-open asset tab without discarding pending edits or history in other tabs.")]
    public async Task<IReadOnlyList<WorkspaceDocumentTab>> ActivateDocument(string kind,string path,CancellationToken cancellationToken)
    {
        await ui.InvokeAsync(async()=>{await capture.View.ActivateDocumentAsync(kind,path);return true;},cancellationToken);
        return await ui.Invoke(capture.View.InspectOpenDocuments,cancellationToken);
    }
    [McpServerTool(Name="close_document"),Description("Close one retained asset tab. Refuses dirty documents; save or undo their changes first.")]
    public async Task<IReadOnlyList<WorkspaceDocumentTab>> CloseDocument(string kind,string path,CancellationToken cancellationToken)
    {
        await ui.InvokeAsync(async()=>{await capture.View.CloseDocumentAsync(kind,path);return true;},cancellationToken);
        return await ui.Invoke(capture.View.InspectOpenDocuments,cancellationToken);
    }
    [McpServerTool(Name="save_all_documents"),Description("Save all dirty asset tabs through their native save guards, then restore the active tab. Stops at a conflict; earlier saves remain saved.")]
    public async Task<IReadOnlyList<WorkspaceDocumentTab>> SaveAllDocuments(CancellationToken cancellationToken)
    {
        await ui.InvokeAsync(async()=>{await capture.View.SaveAllDocumentsAsync();return true;},cancellationToken);
        return await ui.Invoke(capture.View.InspectOpenDocuments,cancellationToken);
    }
}
