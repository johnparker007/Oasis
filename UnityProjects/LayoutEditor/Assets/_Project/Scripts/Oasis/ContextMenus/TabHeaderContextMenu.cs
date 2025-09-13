using NativeWindowsContextMenu;
using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;

// Context menu for tab headers.
public sealed class TabHeaderContextMenu : NativeContextMenu
{
    bool _maximised;
    byte[] _savedBytes;
    PanelHeader _panelHeader;
    PanelResizeHelper[] _resizeHelpers;
    PanelTab[] _tabs;

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

                        _panelHeader = panel.GetComponentInChildren<PanelHeader>();
                        if (_panelHeader)
                        {
                            _panelHeader.enabled = false;
                        }

                        _resizeHelpers = panel.GetComponentsInChildren<PanelResizeHelper>();
                        foreach (var helper in _resizeHelpers)
                        {
                            if (helper)
                            {
                                helper.enabled = false;
                            }
                        }

                        _tabs = panel.GetComponentsInChildren<PanelTab>();
                        foreach (var t in _tabs)
                        {
                            if (t)
                            {
                                t.enabled = false;
                            }
                        }
                    }
                    else
                    {
                        if (_panelHeader)
                        {
                            _panelHeader.enabled = true;
                        }

                        if (_resizeHelpers != null)
                        {
                            foreach (var helper in _resizeHelpers)
                            {
                                if (helper)
                                {
                                    helper.enabled = true;
                                }
                            }
                        }

                        if (_tabs != null)
                        {
                            foreach (var t in _tabs)
                            {
                                if (t)
                                {
                                    t.enabled = true;
                                }
                            }
                        }

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
