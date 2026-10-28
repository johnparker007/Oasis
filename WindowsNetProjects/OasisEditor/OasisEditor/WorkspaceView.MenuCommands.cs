using Avalonia.Controls;
using Dock.Model.Core;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private async Task CreateAssetFromMenuAsync(string kind)
    {
        if (_session.ProjectFilePath is null)
        {
            SetStatus("Open or create a project before creating an asset.");
            return;
        }

        switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "panel":
            case "panel 2d":
            case "panel2d":
                await CreateNamedProjectAssetFromMenuAsync("Panel2D", "New Panel2D", _session.CreatePanel, ShowPanel);
                break;
            case "face":
                await CreateNamedProjectAssetFromMenuAsync("Face", "New Face", name => _faces.CreateFace(name), ShowFace);
                break;
            case "cabinet":
            case "cabinet 3d":
            case "cabinet3d":
                await Create3DAssetDialog(prefab: false);
                break;
            case "machine":
                await NewMachineDialog();
                break;
            case "reel":
                await CreateReelDialogAsync();
                break;
            case "prefab":
            case "object 3d":
            case "object3d":
                await Create3DAssetDialog(prefab: true);
                break;
            default:
                SetStatus($"Asset type '{kind}' is not supported by the Create menu.");
                break;
        }
    }

    private async Task CreateNamedProjectAssetFromMenuAsync(
        string assetKind,
        string dialogTitle,
        Func<string, string> create,
        Action showAsset)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var name = new TextBox { PlaceholderText = $"{assetKind} name" };
        var message = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var content = new StackPanel { Margin = new global::Avalonia.Thickness(18), Spacing = 10 };
        content.Children.Add(name);
        content.Children.Add(message);
        var dialog = new Window { Title = dialogTitle, Width = 420, Height = 190, Content = content, CanResize = false };
        content.Children.Add(ActionButton($"Create {assetKind}", () =>
        {
            try
            {
                var path = create(name.Text ?? string.Empty);
                showAsset();
                SetStatus($"Created {assetKind} package: {path}");
                dialog.Close();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                message.Text = error.Message;
            }
        }));
        await dialog.ShowDialog(owner);
    }

    private void SetSelectionObjectState(bool? locked = null, bool? visible = null)
    {
        if (locked is null && visible is null)
            return;
        if (_sceneKind is not null || _machineActive)
        {
            SetStatus("Select Panel2D or Face elements before changing object visibility or locking.");
            return;
        }

        if (_faceActive)
        {
            if (_faces.SelectedId is not { } faceId)
            {
                SetStatus("Select a Face element first.");
                return;
            }

            Try(() => _faces.UpdateElement(faceId, visible: visible, locked: locked));
            return;
        }

        var selected = _session.SelectedIds;
        if (selected.Count == 0)
        {
            SetStatus("Select one or more Panel2D elements first.");
            return;
        }

        Try(() =>
        {
            _session.SetSelectedObjectState(visible: visible, locked: locked);
        });
    }

    private void ShowWorkspacePane(string id)
    {
        if(_documentOwner is {} host){host.ShowWorkspacePane(id);return;}
        var requested = (id ?? string.Empty).Trim();
        var paneId = requested.ToLowerInvariant() switch
        {
            "assets" or "project" => "Project",
            "hierarchy" => "Hierarchy",
            "inspector" => "Inspector",
            "output" => "Output",
            "authoring" or "play" or "play view" or "playview" or "preview" => "Authoring",
            _ => requested
        };

        var leaves = WorkspaceLayoutState.LeafMap(_dock.Layout
            ?? throw new InvalidOperationException("The workspace has no dock layout."));
        if (!leaves.TryGetValue(paneId, out var pane))
        {
            SetStatus(paneId == "Output" && _isAssetWindow
                ? "The Output pane is available in the main workspace."
                : $"Workspace pane '{requested}' was not found.");
            return;
        }

        RestoreMaximisedTab();
        ReopenStandardPane(pane);
    }

    private Task ShowProjectSettingsFromMenuAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || _session.ProjectFilePath is null) return Task.CompletedTask;
        var project = _session.RequireEditorProject();
        var content = new StackPanel { Margin = new global::Avalonia.Thickness(18), Spacing = 10 };
        content.Children.Add(new TextBlock { Text = "Project", FontSize = 18 });
        foreach (var (label, value) in new[] { ("Project name", project.Name), ("Project file", project.ProjectFilePath), ("Assets folder", project.AssetsDirectory) })
        {
            content.Children.Add(new TextBlock { Text = label, Opacity = .7 });
            content.Children.Add(new TextBlock { Text = value, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        }
        content.Children.Add(new TextBlock { Text = "Machine runtime settings are edited from each Machine document.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        var dialog = new Window { Title = "Project Settings", Width = 600, Height = 360, Content = content };
        if (_machines.Document is not null) content.Children.Add(ActionButton("Edit Machine runtime settings", () => { ShowMachine(); ShowWorkspacePane("Inspector"); dialog.Close(); }));
        return dialog.ShowDialog(owner);
    }

    private void ShowInputMapFromMenu()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || _machines.Document is not {} machine) return;
        var content = new StackPanel { Margin = new global::Avalonia.Thickness(18), Spacing = 8 };
        content.Children.Add(new TextBlock { Text = "Input Map — " + machine.DisplayName, FontSize = 18 });
        foreach (var input in machine.InputDefinitions)
            content.Children.Add(new TextBlock { Text = $"{input.Name} · {input.KeyboardShortcut} · {input.ButtonNumber}", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        content.Children.Add(CreateMachineInputDiagnosticsControl());
        var dialog = new Window { Title = "Input Map", Width = 580, Height = 480, Content = new ScrollViewer { Content = content } };
        content.Children.Add(ActionButton("Edit Machine inputs", () => { ShowMachine(); ShowWorkspacePane("Inspector"); dialog.Close(); }));
        if (Preview.Machine is not null) content.Children.Add(ActionButton("Live Machine inputs…", () => { dialog.Close(); ShowPreviewInputs(); }));
        dialog.Show(owner);
    }

    private void ShowPlayViewFromMenu()
    {
        if(_documentOwner is {} host){host.FocusNativeDockable(host._focusedAsset?.Document);_viewport.Focus();return;}
        ShowWorkspacePane("Authoring");
        if (Is3DActive)
            _sceneView.Focus();
        else
            _viewport.Focus();
    }

    private async Task ExitFromMenuAsync() => await RequestEditorExitAsync();
}
