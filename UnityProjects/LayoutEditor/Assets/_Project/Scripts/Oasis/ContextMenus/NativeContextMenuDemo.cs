// --- Demo ---
// Attach this to any UI element (with EventSystem present). Right-click to open a native menu.
using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class NativeContextMenuDemo : MonoBehaviour, IPointerClickHandler
{
    // Demo state to show checked items toggling
    bool _snapToGrid = true;
    bool _angleSnapping = false;

    public void Start()
    {
        // Register a couple of hotkeys globally/runtime (Player builds by default)
        var mgr = EnsureManager();
        mgr.RegisterHotkey(Hotkey.Ctrl('S'), () => Debug.Log("[Hotkey] Save"));
        mgr.RegisterHotkey(Hotkey.Ctrl('O'), () => Debug.Log("[Hotkey] Load"));
    }

    NativeContextMenuManager EnsureManager()
    {
        if (NativeContextMenuManager.Instance != null) return NativeContextMenuManager.Instance;
        var go = new GameObject("NativeContextMenuManager");
        return go.AddComponent<NativeContextMenuManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;

        var mgr = EnsureManager();
        var items = BuildMenu();
        // Optional: register any hotkeys declared inside the menu tree
        mgr.RegisterHotkeysFromMenuTree(items);
        mgr.ShowMenuAtCursor(items);
    }

    List<NativeContextMenuManager.MenuItemSpec> BuildMenu()
    {
        var items = new List<NativeContextMenuManager.MenuItemSpec>();
        var mgr = EnsureManager();

        // File submenu
        var file = new NativeContextMenuManager.MenuItemSpec { Text = "File" };
        file.Children.Add(new NativeContextMenuManager.MenuItemSpec(
            NativeContextMenuManager.WithShortcutText("Load", Hotkey.Ctrl('O')),
            () => Debug.Log("Load chosen"))
        { Shortcut = Hotkey.Ctrl('O') });

        file.Children.Add(new NativeContextMenuManager.MenuItemSpec(
            NativeContextMenuManager.WithShortcutText("Save", Hotkey.Ctrl('S')),
            () => Debug.Log("Save chosen"))
        { Shortcut = Hotkey.Ctrl('S') });

        file.Children.Add(new NativeContextMenuManager.MenuItemSpec("Save As…", () => Debug.Log("Save As chosen"), enabled: false)); // ghosted
        file.Children.Add(NativeContextMenuManager.MenuItemSpec.Sep());

        var recent = new NativeContextMenuManager.MenuItemSpec { Text = "Recent" };
        recent.Children.Add(new NativeContextMenuManager.MenuItemSpec("project_a.scene", () => Debug.Log("Open recent: project_a"), enabled: false));
        recent.Children.Add(new NativeContextMenuManager.MenuItemSpec("project_b.scene", () => Debug.Log("Open recent: project_b")));
        recent.Children.Add(new NativeContextMenuManager.MenuItemSpec("project_c.scene", () => Debug.Log("Open recent: project_c")));
        file.Children.Add(recent);

        file.Children.Add(NativeContextMenuManager.MenuItemSpec.Sep());
        file.Children.Add(new NativeContextMenuManager.MenuItemSpec("Exit", () => Debug.Log("Exit chosen")));

        // Edit submenu
        var edit = new NativeContextMenuManager.MenuItemSpec { Text = "Edit" };
        edit.Children.Add(new NativeContextMenuManager.MenuItemSpec("Undo", () => Debug.Log("Undo"), enabled: false));
        edit.Children.Add(new NativeContextMenuManager.MenuItemSpec("Redo", () => Debug.Log("Redo"), enabled: false));
        edit.Children.Add(NativeContextMenuManager.MenuItemSpec.Sep());

        var snapping = new NativeContextMenuManager.MenuItemSpec { Text = "Snapping" };
        snapping.Children.Add(new NativeContextMenuManager.MenuItemSpec("Snap to Grid", () => { _snapToGrid = !_snapToGrid; Debug.Log($"SnapToGrid={_snapToGrid}"); }, isChecked: _snapToGrid));
        snapping.Children.Add(new NativeContextMenuManager.MenuItemSpec("Angle Snapping", () => { _angleSnapping = !_angleSnapping; Debug.Log($"AngleSnapping={_angleSnapping}"); }, isChecked: _angleSnapping));
        edit.Children.Add(snapping);

        // Root menu (shown as popup)
        items.Add(file);
        items.Add(edit);
        items.Add(NativeContextMenuManager.MenuItemSpec.Sep());
        items.Add(new NativeContextMenuManager.MenuItemSpec("About…", () => Debug.Log("About clicked")));

        return items;
    }
}