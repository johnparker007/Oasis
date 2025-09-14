using UnityEngine;
using DynamicPanels;

// Ensures every PanelTab has the components required for context menu and
// maximise/restore functionality. Adds the components to existing tabs and to
// any tabs created later.
public static class PanelTabComponentAttacher
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        // Attach to existing tabs
        foreach (var tab in Object.FindObjectsOfType<PanelTab>())
        {
            Attach(tab);
        }

        // Listen for future tabs
        PanelNotificationCenter.OnTabCreated -= Attach;
        PanelNotificationCenter.OnTabCreated += Attach;
    }

    static void Attach(PanelTab tab)
    {
        if (!tab)
        {
            return;
        }

        if (tab.GetComponent<TabHeaderContextMenu>() == null)
        {
            tab.gameObject.AddComponent<TabHeaderContextMenu>();
        }

        if (tab.GetComponent<PanelTabMaximiser>() == null)
        {
            tab.gameObject.AddComponent<PanelTabMaximiser>();
        }

        if (tab.GetComponent<PanelTabDoubleClickMaximiser>() == null)
        {
            tab.gameObject.AddComponent<PanelTabDoubleClickMaximiser>();
        }

        if (tab.GetComponent<PanelTabMiddleClickCloser>() == null)
        {
            tab.gameObject.AddComponent<PanelTabMiddleClickCloser>();
        }
    }
}
