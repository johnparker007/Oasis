using NativeWindowsContextMenu;
using System;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;
using Oasis;
using Oasis.Layout;
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
        bool baseViewActive = Editor.Instance.TabController.IsTabActive(TabController.TabTypes.BaseView);

        LayoutObject layout = Editor.Instance?.Project?.Layout;

        var addTabChildren = new List<NativeContextMenuManager.MenuItemSpec>
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
        };

        if (layout != null)
        {
            IReadOnlyList<View> views = layout.GetViews();
            if (views != null)
            {
                foreach (View view in views)
                {
                    if (view == null)
                    {
                        continue;
                    }

                    string viewName = view.Name;
                    if (string.IsNullOrWhiteSpace(viewName) || string.Equals(viewName, ViewController.kBaseViewName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool viewActive = IsViewActive(viewName);

                    addTabChildren.Add(new NativeContextMenuManager.MenuItemSpec(
                        viewName,
                        () =>
                        {
                            layout.TryEnsureViewTab(view, TabController.TabTypes.TestNewView, anchorPanel);
                        },
                        !viewActive));
                }
            }
        }

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
                Children = addTabChildren
            },
        };
    }

    private static bool IsViewActive(string viewName)
    {
        EditorView editorView = ViewController.GetEditorView(viewName);
        if (editorView == null)
        {
            return false;
        }

        return editorView.isActiveAndEnabled && editorView.gameObject.activeInHierarchy;
    }
}
