using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_unreferenced_assets", ReadOnly = true), Description("Preview loose media and orphaned FML output eligible for cleanup. Preserves saved packages, Library and every open undo/redo state. Does not delete anything.")]
    public Task<object> InspectUnreferencedAssets(CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            var plan = await capture.View.InspectUnreferencedAssetsAsync();
            capture.AssetCleanupPreview = plan;
            return new { scanId = plan.Id, count = plan.Candidates.Count, candidates = plan.Candidates.Take(1000).Select(c => new { c.Path, c.Bytes, c.IsDirectory }) };
        }, cancellationToken);

    [McpServerTool(Name = "delete_unreferenced_assets"), Description("Delete only explicitly selected relative paths from the latest cleanup scanId. Revalidates saved references, open history and file contents; stale previews abort before any deletion.")]
    public Task<object> DeleteUnreferencedAssets(string scanId, string[] paths, CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            var plan = capture.AssetCleanupPreview;
            if (plan is null || plan.Id != scanId) throw new InvalidOperationException("Inspect unreferenced assets again before deleting.");
            var result = capture.View.DeleteUnreferencedAssets(plan, paths);
            capture.AssetCleanupPreview = null;
            return result;
        }, cancellationToken);
}
