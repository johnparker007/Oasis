#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Oasis;

public static class TabPoolMenu
{
    [MenuItem("Window/Restore Tabs/Show Inspector")]
    private static void ShowInspector()
    {
        TabPool pool = Object.FindObjectOfType<TabPool>();
        if (pool != null)
        {
            pool.ShowTab("Inspector");
        }
        else
        {
            Debug.LogWarning("TabPool not found in the scene.");
        }
    }
}
#endif
