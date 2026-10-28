using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_project_asset_package", ReadOnly = true), Description("Return the saved package's current fingerprint for detecting independent file changes before a context action. Requires a project asset manifest path.")]
    public Task<object> InspectProjectAssetPackage(string manifestPath, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { manifestPath, fingerprint = capture.View.GetProjectAssetPackageFingerprint(manifestPath) }, cancellationToken);

    [McpServerTool(Name = "rename_project_asset_package"), Description("Rename one saved project asset package without changing its manifest or native IDs. Refuses open, dirty, referenced, changed or conflicting packages. Supply the fingerprint from inspect_project_asset_package to detect external edits.")]
    public Task<object> RenameProjectAssetPackage(string manifestPath, string packageName, CancellationToken cancellationToken, string? expectedFingerprint = null)
        => ui.Invoke<object>(() =>
        {
            var result = capture.View.RenameProjectAssetPackage(manifestPath, packageName, expectedFingerprint);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            return new { result.ManifestPath, result.RecoveryPath, assets = rootSession.ListAssets() };
        }, cancellationToken);

    [McpServerTool(Name = "delete_project_asset_package"), Description("Move one saved project asset package to Generated/Trash for recovery. Set confirm=true to proceed. Refuses open, dirty, referenced, changed or unsafe packages.")]
    public Task<object> DeleteProjectAssetPackage(string manifestPath, bool confirm, CancellationToken cancellationToken, string? expectedFingerprint = null)
        => ui.Invoke<object>(() =>
        {
            if (!confirm) throw new InvalidOperationException("Deletion was not confirmed. Call again with confirm=true after reviewing the selected package.");
            var result = capture.View.MoveProjectAssetPackageToTrash(manifestPath, expectedFingerprint);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            return new { result.ManifestPath, result.RecoveryPath, assets = rootSession.ListAssets() };
        }, cancellationToken);
}
