using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using OasisEditor.Authoring.Assets;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    public string GetProjectAssetPackageFingerprint(string manifestPath)
    {
        EnsureProjectPackageCanChange(manifestPath);
        var project = _session.RequireEditorProject();
        return ProjectAssetPackageService.GetFingerprint(project, ResolveSelectedPackage(project, manifestPath));
    }

    public ProjectAssetPackageResult RenameProjectAssetPackage(string manifestPath, string name, string? expectedFingerprint = null)
    {
        EnsureProjectPackageCanChange(manifestPath);
        var project = _session.RequireEditorProject();
        var result = ProjectAssetPackageService.Rename(project, ResolveSelectedPackage(project, manifestPath), name, expectedFingerprint);
        if (result.Success) { Refresh(); SetStatus("Asset package renamed to " + Path.GetFileName(Path.GetDirectoryName(result.ManifestPath!)) + "."); }
        return result;
    }

    public ProjectAssetPackageResult MoveProjectAssetPackageToTrash(string manifestPath, string? expectedFingerprint = null)
    {
        EnsureProjectPackageCanChange(manifestPath);
        var project = _session.RequireEditorProject();
        var result = ProjectAssetPackageService.MoveToTrash(project, ResolveSelectedPackage(project, manifestPath), expectedFingerprint);
        if (result.Success) { Refresh(); SetStatus("Asset moved to recoverable Trash: " + result.RecoveryPath); }
        return result;
    }

    /// <summary>Call from the Project browser setup to add actions for saved project packages.</summary>
    public void InitializeProjectAssetContextActions()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ContextAction("Reveal in File Explorer", () => RevealProjectAsset(_assets.SelectedItem as AssetSummary)));
        menu.Items.Add(ContextAction("Rename…", (Func<Task>)(() => RenameProjectAssetAsync(_assets.SelectedItem as AssetSummary))));
        menu.Items.Add(ContextAction("Delete…", (Func<Task>)(() => DeleteProjectAssetAsync(_assets.SelectedItem as AssetSummary))));
        _assets.ContextMenu = menu;
        _assets.ContextRequested += (_, e) =>
        {
            if (_assets.SelectedItem is not AssetSummary) e.Handled = true;
            foreach (var item in menu.Items.OfType<MenuItem>()) item.IsEnabled = !_browseLibrary && _assets.SelectedItem is AssetSummary;
        };
    }

    public void RevealProjectAsset(AssetSummary? asset)
    {
        try
        {
            var project = _session.RequireEditorProject();
            if (_browseLibrary || asset is null) throw new InvalidOperationException("Select a saved project asset first.");
            var manifest = ResolveSelectedPackage(project, asset.ManifestPath);
            var start = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{manifest}\"") { UseShellExecute = true }
                : new ProcessStartInfo(Path.GetDirectoryName(manifest)!) { UseShellExecute = true };
            Process.Start(start);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { SetStatus("Could not reveal asset: " + error.Message); }
    }

    public async Task RenameProjectAssetAsync(AssetSummary? asset)
    {
        if (_browseLibrary || asset is null) { SetStatus("Select a saved project asset first."); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        try
        {
            EnsureProjectPackageCanChange(asset.ManifestPath);
            var project = _session.RequireEditorProject();
            var manifest = ResolveSelectedPackage(project, asset.ManifestPath);
            var fingerprint = ProjectAssetPackageService.GetFingerprint(project, manifest);
            var package = Path.GetDirectoryName(manifest)!;
            var name = new TextBox { Text = Path.GetFileName(package) };
            var message = new TextBlock { Text = "The package contents and native asset IDs will be preserved.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            var dialog = new Window { Title = "Rename asset package", Width = 440, Height = 220, CanResize = false };
            var body = new StackPanel { Margin = new Thickness(18), Spacing = 10, Children = { new TextBlock { Text = "Package name" }, name, message } };
            body.Children.Add(ActionButton("Rename", () =>
            {
                try
                {
                    EnsureProjectPackageCanChange(asset.ManifestPath);
                    var result = RenameProjectAssetPackage(manifest, name.Text ?? string.Empty, fingerprint);
                    if (!result.Success) { message.Text = result.Error; return; }
                    var updatedPath = Path.GetFullPath(result.ManifestPath!);
                    _assets.SelectedItem = _session.ListAssets().FirstOrDefault(item => SameScenePath(Path.GetFullPath(item.ManifestPath, project.ProjectDirectory), updatedPath));
                    dialog.Close();
                }
                catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
                { message.Text = error.Message; }
            }));
            dialog.Content = body;
            await dialog.ShowDialog(owner);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        { SetStatus("Rename blocked: " + error.Message); }
    }

    public async Task DeleteProjectAssetAsync(AssetSummary? asset)
    {
        if (_browseLibrary || asset is null) { SetStatus("Select a saved project asset first."); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        try
        {
            EnsureProjectPackageCanChange(asset.ManifestPath);
            var project = _session.RequireEditorProject();
            var manifest = ResolveSelectedPackage(project, asset.ManifestPath);
            var fingerprint = ProjectAssetPackageService.GetFingerprint(project, manifest);
            var message = new TextBlock { Text = "This moves the package into Generated/Trash so it can be recovered. Referenced or open packages cannot be removed.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            var dialog = new Window { Title = "Delete asset package", Width = 480, Height = 230, CanResize = false };
            var body = new StackPanel { Margin = new Thickness(18), Spacing = 12, Children = { message, new TextBlock { Text = Path.GetFileName(Path.GetDirectoryName(manifest)) } } };
            body.Children.Add(ActionButton("Move to Trash", () =>
            {
                try
                {
                    EnsureProjectPackageCanChange(asset.ManifestPath);
                    var result = MoveProjectAssetPackageToTrash(manifest, fingerprint);
                    if (!result.Success) { message.Text = result.Error; return; }
                    dialog.Close();
                }
                catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
                { message.Text = error.Message; }
            }));
            body.Children.Add(ActionButton("Cancel", () => dialog.Close()));
            dialog.Content = body;
            await dialog.ShowDialog(owner);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        { SetStatus("Delete blocked: " + error.Message); }
    }

    private void EnsureProjectPackageCanChange(string manifestPath)
    {
        if (HasUnsavedChanges) throw new InvalidOperationException("Save or undo open document edits before changing a project package.");
        var project = _session.RequireEditorProject();
        var manifest = ResolveSelectedPackage(project, manifestPath);
        var package = Path.GetDirectoryName(manifest)!;
        foreach (var document in InspectOpenDocuments())
        {
            var openPackage = Path.GetDirectoryName(Path.GetFullPath(document.Path, project.ProjectDirectory));
            if (string.Equals(openPackage, package, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                var floating = document.IsFloating ? "floating " : string.Empty;
                throw new InvalidOperationException($"'{Path.GetFileName(package)}' is open in the {floating}{document.Kind} editor. Close it before changing the package.");
            }
        }
    }

    private static string ResolveSelectedPackage(EditorProject project, string manifestPath)
    {
        var path = Path.IsPathRooted(manifestPath) ? Path.GetFullPath(manifestPath) : Path.GetFullPath(manifestPath, project.ProjectDirectory);
        var relative = Path.GetRelativePath(project.AssetsDirectory, path);
        return WorkspacePathPolicy.ResolveConfined(project.AssetsDirectory, relative, requireExists: true);
    }

    private static MenuItem ContextAction(string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        return item;
    }
    private static MenuItem ContextAction(string label, Func<Task> action)
    {
        var item = new MenuItem { Header = label };
        item.Click += async (_, _) => await action();
        return item;
    }
}
