using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;
using Oasis;
using Oasis.LayoutEditor;

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
                        Editor.Instance.TabController.HideTab(tab);
                    }
                }),
            NativeContextMenuManager.MenuItemSpec.Sep(),
            new NativeContextMenuManager.MenuItemSpec(
                "Add Tab")
            {
                Children = new List<NativeContextMenuManager.MenuItemSpec>
                {
                    new NativeContextMenuManager.MenuItemSpec(
                        "Hierarchy",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Hierarchy);
                        }),
                    new NativeContextMenuManager.MenuItemSpec(
                        "Inspector",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Inspector);
                        }),
                    new NativeContextMenuManager.MenuItemSpec(
                        "Project",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Project);
                        }),
                }
            },
        };
    }
}
