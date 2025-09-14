using System.Collections.Generic;
using UnityEngine;
using DynamicPanels;
using Oasis.LayoutEditor;

namespace Oasis
{
    public class TabPool : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _closedTabsRoot;

        [SerializeField]
        private TabController.TabTypes[] _toolWindowIds;

        private readonly Dictionary<TabController.TabTypes, Panel> _storedPanels = new Dictionary<TabController.TabTypes, Panel>();

        private void Awake()
        {
            PanelNotificationCenter.OnTabClosed += HandleTabClosed;
        }

        private void OnDestroy()
        {
            PanelNotificationCenter.OnTabClosed -= HandleTabClosed;
        }

        private void HandleTabClosed(PanelTab tab)
        {
            if (tab == null)
            {
                return;
            }

            string id = tab.ID;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (!System.Enum.TryParse<TabController.TabTypes>(id, out var tabType))
            {
                return;
            }

            if (_toolWindowIds != null && _toolWindowIds.Length > 0)
            {
                bool match = false;
                for (int i = 0; i < _toolWindowIds.Length; i++)
                {
                    if (_toolWindowIds[i] == tabType)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    return;
                }
            }

            Panel panel = tab.Panel.DetachTab(tab);
            if (panel == null)
            {
                return;
            }

            if (_closedTabsRoot != null)
            {
                panel.RectTransform.SetParent(_closedTabsRoot, false);
            }

            panel.gameObject.SetActive(false);
            _storedPanels[tabType] = panel;
        }

        public void ShowTab(TabController.TabTypes id)
        {
            Panel panel;
            if (!_storedPanels.TryGetValue(id, out panel) || panel == null)
            {
                return;
            }

            panel.gameObject.SetActive(true);
            PanelManager.Instance.AnchorPanel(panel, panel.Canvas, Direction.Right);
            _storedPanels.Remove(id);
        }

        public void ShowInspector()
        {
            ShowTab(TabController.TabTypes.Inspector);
        }
    }
}

