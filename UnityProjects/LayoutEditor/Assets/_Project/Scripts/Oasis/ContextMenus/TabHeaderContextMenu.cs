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
        Panel anchorPanel = tab ? tab.Panel : null;

        bool hierarchyActive = Editor.Instance.TabController.IsTabActive(TabController.TabTypes.Hierarchy);
        bool inspectorActive = Editor.Instance.TabController.IsTabActive(TabController.TabTypes.Inspector);
        bool projectActive = Editor.Instance.TabController.IsTabActive(TabController.TabTypes.Project);

        // To improve - the whole 'Views' thing needs to be dynamic from list of available views in this project
        bool baseViewActive = Editor.Instance.TabController.IsTabActive(TabController.TabTypes.BaseView);

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
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Hierarchy, anchorPanel);
                        },
                        !hierarchyActive),
                    new NativeContextMenuManager.MenuItemSpec(
                        "Inspector",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Inspector, anchorPanel);
                        },
                        !inspectorActive),
                    new NativeContextMenuManager.MenuItemSpec(
                        "Project",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Project, anchorPanel);
                        },
                        !projectActive),
                    NativeContextMenuManager.MenuItemSpec.Sep(),
                    new NativeContextMenuManager.MenuItemSpec(
                        "Base View",
                        () =>
                        {
                            Editor.Instance.TabController.ShowTab(TabController.TabTypes.BaseView, anchorPanel);
                        },
                        !baseViewActive),
                }
            },
        };
    }
}
