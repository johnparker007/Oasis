using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;

// Context menu for tab headers.
public sealed class TabHeaderContextMenu : NativeContextMenu
{
    bool _maximised;
    byte[] _savedBytes;

    protected override List<NativeContextMenuManager.MenuItemSpec> BuildMenu()
    {
        var tab = GetComponent<PanelTab>();
        var panel = tab ? tab.Panel : null;

        return new List<NativeContextMenuManager.MenuItemSpec>
        {
            new NativeContextMenuManager.MenuItemSpec(
                "Maximise",
                () =>
                {
                    if (!panel)
                    {
                        return;
                    }

                    _maximised = !_maximised;
                    if (_maximised)
                    {
                        _savedBytes = PanelSerialization.SerializeCanvasToArray(panel.Canvas);
                        panel.Detach();
                        panel.BringForward();
                        panel.RectTransform.anchoredPosition = Vector2.zero;
                        panel.FloatingSize = panel.Canvas.Size;
                    }
                    else
                    {
                        PanelSerialization.DeserializeCanvasFromArray(panel.Canvas, _savedBytes);
                    }
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
