using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;

// Context menu for tab headers.
public sealed class TabHeaderContextMenu : NativeContextMenu
{
    protected override List<NativeContextMenuManager.MenuItemSpec> BuildMenu()
    {
        return new List<NativeContextMenuManager.MenuItemSpec>
        {
            new NativeContextMenuManager.MenuItemSpec("Maximise", () => Debug.Log("Maximise clicked")),
            new NativeContextMenuManager.MenuItemSpec("Close", () => Debug.Log("Close clicked")),
        };
    }
}
