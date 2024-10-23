using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
using System;


namespace Oasis.LayoutEditor
{
    public class ViewController : MonoBehaviour
    {
        public PanelTab ViewMamePanelTab
        {
            get;
            private set;
        } = null;

        public void AddViewMame()
        {
            ViewMamePanelTab = Editor.Instance.TabController.ShowTab(TabController.TabTypes.MameView);
        }

    }

}
