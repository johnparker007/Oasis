using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    // Called by the main constructor after the Edit menu and scene toolbar are created.
    private void InitializeClipboardUi(MenuItem editMenu, StackPanel sceneTools)
    {
        MenuAction(editMenu, "Copy", CopyPanelSelection);
        MenuAction(editMenu, "Cut", CutPanelSelection);
        MenuAction(editMenu, "Paste", PastePanelSelection);
        MenuAction(editMenu, "Bring to front", () => Try(() => { RequirePanel(); _session.BringSelectionToFront(); }));
        MenuAction(editMenu, "Send to back", () => Try(() => { RequirePanel(); _session.SendSelectionToBack(); }));
        sceneTools.Children.Add(ActionButton("Copy", CopyPanelSelection));
        sceneTools.Children.Add(ActionButton("Paste", PastePanelSelection));

        KeyDown += (_, e) =>
        {
            var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (!command || e.Source is TextBox) return;
            if (e.Key == Key.C) { CopyPanelSelection(); e.Handled = true; }
            else if (e.Key == Key.X) { CutPanelSelection(); e.Handled = true; }
            else if (e.Key == Key.V) { PastePanelSelection(); e.Handled = true; }
        };
    }

    private void CopyPanelSelection() => Try(() => { RequirePanel(); _session.CopySelectedToClipboard(); });
    private void CutPanelSelection() => Try(() => { RequirePanel(); _session.CutSelectedToClipboard(); });
    private void PastePanelSelection() => Try(() => { RequirePanel(); _session.PasteClipboard(); });
}
