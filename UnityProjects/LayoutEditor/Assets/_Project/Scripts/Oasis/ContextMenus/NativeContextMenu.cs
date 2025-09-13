using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Base class for native context menus. Attach to any UI element (with EventSystem present)
// to show a native Windows context menu on right-click.
public abstract class NativeContextMenu : MonoBehaviour, IPointerClickHandler
{
    protected NativeContextMenuManager EnsureManager()
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

    // Build the menu items for this context menu.
    protected abstract List<NativeContextMenuManager.MenuItemSpec> BuildMenu();
}
