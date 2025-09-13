using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;

// Context menu for tab headers.
public sealed class TabHeaderContextMenu : NativeContextMenu
{
    protected override List<NativeContextMenuManager.MenuItemSpec> BuildMenu()
    {
        var tab = GetComponent<PanelTab>();

        return new List<NativeContextMenuManager.MenuItemSpec>
        {
            new NativeContextMenuManager.MenuItemSpec("Maximise", () => Debug.Log("Maximise clicked")),
            new NativeContextMenuManager.MenuItemSpec(
                "Close",
                () =>
                {
                    if (tab)
                    {
                        tab.Destroy();
                    }
                }),
        };
    }
}
