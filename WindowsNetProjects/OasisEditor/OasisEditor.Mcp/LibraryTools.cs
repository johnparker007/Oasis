using System.ComponentModel;
using ModelContextProtocol.Server;
using OasisEditor;
using OasisEditor.Authoring.Assets;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    private static object LibraryCatalogSnapshot(LibraryDiscoveryResult catalog) => new
    {
        assets = catalog.Assets.Select(asset => new
        {
            type = asset.Type.ToString(),
            asset.DisplayName,
            reference = new { scope = asset.Reference.Scope.ToString(), asset.Reference.Path },
            asset.ManifestPath
        }),
        diagnostics = catalog.Diagnostics
    };

    [McpServerTool(Name = "inspect_library", ReadOnly = true), Description("Inspect the explicitly configured Oasis Library root, its current discovered asset catalog and diagnostics without changing or rescanning files.")]
    public Task<object> InspectLibrary(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { libraryRoot = capture.View.SceneLibraryRoot, catalog = LibraryCatalogSnapshot(capture.View.LibraryCatalog) }, cancellationToken);

    [McpServerTool(Name = "list_library_assets", ReadOnly = true), Description("Refresh and list valid reusable Cabinet3D, Reel and Object3D assets from the configured Oasis Library, including discovery diagnostics.")]
    public Task<object> ListLibraryAssets(CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            await capture.View.RefreshLibraryAsync();
            return new { libraryRoot = capture.View.SceneLibraryRoot, catalog = LibraryCatalogSnapshot(capture.View.LibraryCatalog) };
        }, cancellationToken);

    [McpServerTool(Name = "configure_library_root"), Description("Set an existing directory as the Oasis Library root and refresh its asset catalog. The tool does not create the directory.")]
    public Task<object> ConfigureLibraryRoot(string directory, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            capture.View.SetSceneLibraryRoot(directory);
            await capture.View.RefreshLibraryAsync();
            return new { libraryRoot = capture.View.SceneLibraryRoot, catalog = LibraryCatalogSnapshot(capture.View.LibraryCatalog) };
        }, cancellationToken);

    [McpServerTool(Name = "open_library_asset"), Description("Open an existing Library Reel, Cabinet3D or Object3D asset by its Library-relative manifest path and load its native authoring document.")]
    public Task<object> OpenLibraryAsset(string libraryRelativeManifest, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            await capture.View.OpenLibraryAssetAsync(libraryRelativeManifest);
            await capture.View.SynchronizeAssetDocumentsAsync();
            capture.View.SetAssetBrowserSource(library: true);
            await capture.View.CommandView.WaitForSceneAsync();
            return new
            {
                libraryRoot = capture.View.SceneLibraryRoot,
                sceneKind = capture.View.CommandView.SceneKind,
                scene = Scene3DSnapshot()
            };
        }, cancellationToken);

    [McpServerTool(Name = "copy_asset_to_library"), Description("Copy an existing project Cabinet3D, Reel or Object3D package into the configured Library using the native package copier. Existing destinations are preserved and rejected.")]
    public Task<object> CopyAssetToLibrary(string projectManifest, CancellationToken cancellationToken, string? packageName = null)
        => ui.InvokeAsync<object>(async () =>
        {
            var result = capture.View.CopyProjectAssetToLibrary(projectManifest, packageName);
            await capture.View.RefreshLibraryAsync();
            return new { result, libraryRoot = capture.View.SceneLibraryRoot, catalog = LibraryCatalogSnapshot(capture.View.LibraryCatalog) };
        }, cancellationToken);

    [McpServerTool(Name = "import_library_asset"), Description("Copy an existing Library Cabinet3D, Reel or Object3D package into the current project's matching Assets folder using the native package copier.")]
    public Task<object> ImportLibraryAsset(string libraryRelativeManifest, CancellationToken cancellationToken, string? packageName = null)
        => ui.InvokeAsync<object>(async () =>
        {
            var result = capture.View.ImportLibraryAsset(libraryRelativeManifest, packageName);
            await capture.View.RefreshLibraryAsync();
            return new { result, libraryRoot = capture.View.SceneLibraryRoot, catalog = LibraryCatalogSnapshot(capture.View.LibraryCatalog) };
        }, cancellationToken);

    [McpServerTool(Name = "list_reusable_asset_choices", ReadOnly = true), Description("List the valid project and Library asset choices for Cabinet3D, Reel or Object3D assignments, with an optional current reference to preserve when missing.")]
    public Task<object> ListReusableAssetChoices(EditorAssetType type, CancellationToken cancellationToken, AssetReference? current = null)
        => ui.Invoke<object>(() => new
        {
            type = type.ToString(),
            choices = capture.View.GetReusableAssetChoices(type, current).Select(choice => new
            {
                choice.Label,
                reference = choice.Reference is null ? null : new { scope = choice.Reference.Scope.ToString(), choice.Reference.Path }
            })
        }, cancellationToken);

    [McpServerTool(Name = "assign_machine_reel"), Description("Assign or clear one native Reel asset on a Machine reel reference such as reel:0. Non-null paths are portable and scoped to Project or Library; changes use Machine undo history.")]
    public Task<object> AssignMachineReel(string reelReference, CancellationToken cancellationToken, string? assetPath = null, string scope = "Project")
        => ui.Invoke<object>(() =>
        {
            if (!MachineObjectReference.TryParse(reelReference, out var parsed) || parsed.Kind != MachineObjectKind.Reel)
                throw new ArgumentException("Machine Reel reference must be a valid reference such as reel:0.", nameof(reelReference));

            AssetReference? asset = null;
            if (assetPath is not null)
            {
                if (!Enum.TryParse<AssetReferenceScope>(scope, ignoreCase: true, out var parsedScope) || !Enum.IsDefined(parsedScope))
                    throw new ArgumentException("Asset scope must be Project or Library.", nameof(scope));
                asset = new AssetReference(parsedScope, assetPath);
            }

            capture.View.AssignMachineReel(parsed, asset);
            return MachineSnapshot();
        }, cancellationToken);
}
