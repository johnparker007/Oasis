using UnityEngine;
using Oasis;
using Oasis.LayoutEditor;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnWindowShowHierarchy()
        {
            ShowWindowTab(TabController.TabTypes.Hierarchy);
        }

        public void OnWindowShowInspector()
        {
            ShowWindowTab(TabController.TabTypes.Inspector);
        }

        public void OnWindowShowProject()
        {
            ShowWindowTab(TabController.TabTypes.Project);
        }

        public void OnWindowShowBaseView()
        {
            ShowWindowTab(TabController.TabTypes.BaseView);
        }

        public bool CanShowWindowHierarchy()
        {
            return CanShowWindowTab(TabController.TabTypes.Hierarchy);
        }

        public bool CanShowWindowInspector()
        {
            return CanShowWindowTab(TabController.TabTypes.Inspector);
        }

        public bool CanShowWindowProject()
        {
            return CanShowWindowTab(TabController.TabTypes.Project);
        }

        public bool CanShowWindowBaseView()
        {
            return CanShowWindowTab(TabController.TabTypes.BaseView);
        }

        private void ShowWindowTab(TabController.TabTypes tabType)
        {
            Editor.Instance.TabController.ShowTab(tabType);
        }

        private bool CanShowWindowTab(TabController.TabTypes tabType)
        {
            return !Editor.Instance.TabController.IsTabActive(tabType);
        }
    }
}
