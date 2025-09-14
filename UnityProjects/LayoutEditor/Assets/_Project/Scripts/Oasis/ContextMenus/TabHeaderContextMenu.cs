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
        var maximiser = GetComponent<PanelTabMaximiser>();

        return new List<NativeContextMenuManager.MenuItemSpec>
        {
            new NativeContextMenuManager.MenuItemSpec(
                "Maximise",
                () =>
                {
                    if (maximiser)
                    {
                        maximiser.ToggleMaximise();
                    }
                },
                true,
                maximiser && maximiser.IsMaximised),
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
