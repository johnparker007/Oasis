using UnityEngine;
using DynamicPanels;

// Ensures every PanelTab has a TabHeaderContextMenu component attached.
// Adds the component to existing tabs and to any tabs created later.
public static class TabHeaderContextMenuAttacher
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
        if (tab && tab.GetComponent<TabHeaderContextMenu>() == null)
        {
            tab.gameObject.AddComponent<TabHeaderContextMenu>();
        }
    }
}
