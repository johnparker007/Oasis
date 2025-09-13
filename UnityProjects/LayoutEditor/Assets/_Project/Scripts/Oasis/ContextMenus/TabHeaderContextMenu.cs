using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;

// Context menu for tab headers.
public sealed class TabHeaderContextMenu : NativeContextMenu
{
    bool _maximised;

    protected override List<NativeContextMenuManager.MenuItemSpec> BuildMenu()
    {
        var tab = GetComponent<PanelTab>();

        return new List<NativeContextMenuManager.MenuItemSpec>
        {
            new NativeContextMenuManager.MenuItemSpec(
                "Maximise",
                () =>
                {
                    _maximised = !_maximised;
                    Debug.Log($"Maximise set to {_maximised}");
                },
                true,
                _maximised),
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
