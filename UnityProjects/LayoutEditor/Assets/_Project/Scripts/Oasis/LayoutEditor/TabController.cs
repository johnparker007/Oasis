using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;

namespace Oasis.LayoutEditor
{
    public class TabController : MonoBehaviour
    {
        public enum TabTypes
        {
            BaseView,
            MameView,
            CustomView,
            ProjectSettings,
            Preferences,
            LampRemapper,
            LocalLightNormaliser,
            UpscaledBase,
            Hierarchy,
            Inspector,
            Project
        }

        [Serializable]
        public class TabDefinition
        {
            public string Label;
            [FormerlySerializedAs("TabType")]
            public TabTypes TypeID;
            public Sprite Icon;
            public RectTransform RectTransform;
        }

        [SerializeField]
        private RectTransform _closedTabsRoot;

        private readonly Dictionary<TabTypes, Panel> _storedPanels = new();

        public List<TabDefinition> TabDefinitions;

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
            HideTab(tab);
        }

        public void HideTab(PanelTab tab)
        {
            if (tab == null)
            {
                return;
            }

            if (!Enum.TryParse(tab.ID, out TabTypes id))
            {
                return;
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

        public PanelTab ShowTab(TabTypes tabType)
        {
            if (tabType == TabTypes.CustomView)
            {
                Debug.LogError("Can't show a custom view just by tab type, needs the uniquely identifying name (label)!");
                return null;
            }

            if (_storedPanels.TryGetValue(tabType, out Panel storedPanel) && storedPanel != null)
            {
                storedPanel.gameObject.SetActive(true);
                PanelManager.Instance.AnchorPanel(storedPanel, storedPanel.Canvas, Direction.Right);
                _storedPanels.Remove(tabType);
                return storedPanel[0];
            }

            TabDefinition tabDefinition = TabDefinitions.Find(x => x.TypeID == tabType);
            PanelTab panelTab = CreatePanelTab(tabDefinition);
            return panelTab;
        }

        private PanelTab CreatePanelTab(TabDefinition tabDefinition)
        {
            DynamicPanelsCanvas dynamicPanelsCanvas = Editor.Instance.UIController.DynamicPanelsCanvas;

            Panel panel = PanelUtils.CreatePanelFor(tabDefinition.RectTransform, dynamicPanelsCanvas);
            PanelTab panelTab = panel[0];
            panelTab.Icon = tabDefinition.Icon;
            panelTab.Label = tabDefinition.Label;
            panelTab.ID = tabDefinition.TypeID.ToString();

            return panelTab;
        }
    }

}
