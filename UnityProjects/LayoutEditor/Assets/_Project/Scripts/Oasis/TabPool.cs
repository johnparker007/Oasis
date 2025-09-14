using System;
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
        private TabController.TabTypes[] _toolTabTypes;

        private readonly Dictionary<TabController.TabTypes, Panel> _storedPanels = new();

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

            if (!Enum.TryParse(tab.ID, out TabController.TabTypes id))
            {
                return;
            }

            if (_toolTabTypes != null && _toolTabTypes.Length > 0)
            {
                bool match = false;
                for (int i = 0; i < _toolTabTypes.Length; i++)
                {
                    if (_toolTabTypes[i] == id)
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
            _storedPanels[id] = panel;
        }

        public void ShowTab(TabController.TabTypes tabType)
        {
            Panel panel;
            if (!_storedPanels.TryGetValue(tabType, out panel) || panel == null)
            {
                return;
            }

            panel.gameObject.SetActive(true);
            PanelManager.Instance.AnchorPanel(panel, panel.Canvas, Direction.Right);
            _storedPanels.Remove(tabType);
        }

        public void ShowInspector()
        {
            ShowTab(TabController.TabTypes.Inspector);
        }
    }
}

