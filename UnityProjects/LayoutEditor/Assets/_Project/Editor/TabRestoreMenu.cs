#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Oasis.LayoutEditor;

public static class TabRestoreMenu
{
    [MenuItem("Window/Restore Tabs/Show Inspector")]
    private static void ShowInspector()
    {
        TabController controller = Object.FindObjectOfType<TabController>();
        if (controller != null)
        {
            controller.ShowTab(TabController.TabTypes.Inspector);
        }
        else
        {
            Debug.LogWarning("TabController not found in the scene.");
        }
    }
}
#endif
