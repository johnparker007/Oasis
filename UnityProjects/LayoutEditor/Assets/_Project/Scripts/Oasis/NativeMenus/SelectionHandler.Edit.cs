using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.LayoutEditor;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnEditProjectSettings()
        {
            Editor.Instance.TabController.ShowTab(TabController.TabTypes.ProjectSettings);
        }

        public void OnEditPreferences()
        {
            Editor.Instance.TabController.ShowTab(TabController.TabTypes.Preferences);
        }
    }
}
