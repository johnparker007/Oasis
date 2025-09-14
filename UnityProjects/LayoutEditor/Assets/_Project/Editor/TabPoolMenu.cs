#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Oasis;
using Oasis.LayoutEditor;

public static class TabPoolMenu
{
    [MenuItem("Window/Restore Tabs/Show Inspector")]
    private static void ShowInspector()
    {
        TabPool pool = Object.FindObjectOfType<TabPool>();
        if (pool != null)
        {
            pool.ShowTab(TabController.TabTypes.Inspector);
        }
        else
        {
            Debug.LogWarning("TabPool not found in the scene.");
        }
    }
}
#endif
