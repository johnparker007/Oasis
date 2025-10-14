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
            Project,
            TestNewView
        }

        [Serializable]
        public class TabDefinition
        {
            public string Label;
            [FormerlySerializedAs("TabType")]
            public TabTypes TypeID;
            public Sprite Icon;
            [FormerlySerializedAs("RectTransform")]
            public RectTransform Prefab;
        }

        [SerializeField]
        private RectTransform _closedTabsRoot;

        private readonly Dictionary<TabTypes, Panel> _storedPanels = new();

        public List<TabDefinition> TabDefinitions;

        private void Start()
        {
            PanelTab hierarchyTab = ShowTab(TabTypes.Hierarchy);
            PanelTab inspectorTab = ShowTab(TabTypes.Inspector);
            PanelTab projectTab = ShowTab(TabTypes.Project);

            // need this to load a project (based on an MFME import), will
            // want a better solution later
            PanelTab baseViewTab = ShowTab(TabTypes.BaseView);

            DynamicPanelsCanvas dynamicPanelsCanvas = Editor.Instance.UIController.DynamicPanelsCanvas;
            if (dynamicPanelsCanvas == null)
            {
                Debug.LogWarning("Dynamic panels canvas not found, default tab layout cannot be created.");
                return;
            }

            Panel basePanel = baseViewTab?.Panel;
            if (basePanel == null)
            {
                Debug.LogWarning("Base view tab is missing, default tab layout cannot be created.");
                return;
            }

            DockDefaultLayout(
                dynamicPanelsCanvas,
                basePanel,
                hierarchyTab?.Panel,
                inspectorTab?.Panel,
                projectTab?.Panel);
        }

        private static void DockDefaultLayout(
            DynamicPanelsCanvas canvas,
            Panel basePanel,
            Panel hierarchyPanel,
            Panel inspectorPanel,
            Panel projectPanel)
        {
            RectTransform canvasRect = canvas.RectTransform;
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            PanelManager panelManager = PanelManager.Instance;
            panelManager.AnchorPanel(basePanel, canvas, Direction.Left);

            basePanel.RectTransform.sizeDelta = new Vector2(canvasWidth, canvasHeight);

            const float sideWidthFraction = 0.2f;
            const float projectHeightFraction = 0.25f;

            if (hierarchyPanel != null)
            {
                panelManager.AnchorPanel(hierarchyPanel, basePanel, Direction.Left);
                hierarchyPanel.RectTransform.sizeDelta = new Vector2(canvasWidth * sideWidthFraction, canvasHeight);
            }

            if (inspectorPanel != null)
            {
                panelManager.AnchorPanel(inspectorPanel, basePanel, Direction.Right);
                inspectorPanel.RectTransform.sizeDelta = new Vector2(canvasWidth * sideWidthFraction, canvasHeight);
            }

            float baseWidth = canvasWidth;
            if (hierarchyPanel != null)
            {
                baseWidth -= hierarchyPanel.RectTransform.sizeDelta.x;
            }

            if (inspectorPanel != null)
            {
                baseWidth -= inspectorPanel.RectTransform.sizeDelta.x;
            }

            baseWidth = Mathf.Max(0f, baseWidth);
            basePanel.RectTransform.sizeDelta = new Vector2(baseWidth, canvasHeight);

            float projectHeight = 0f;
            if (projectPanel != null)
            {
                panelManager.AnchorPanel(projectPanel, basePanel, Direction.Bottom);
                projectHeight = canvasHeight * projectHeightFraction;
                projectPanel.RectTransform.sizeDelta = new Vector2(baseWidth, projectHeight);
            }

            float baseHeight = Mathf.Max(0f, canvasHeight - projectHeight);
            basePanel.RectTransform.sizeDelta = new Vector2(baseWidth, baseHeight);

            if (projectPanel != null)
            {
                projectPanel.RectTransform.sizeDelta = new Vector2(baseWidth, projectHeight);
            }
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

        public PanelTab ShowTab(TabTypes tabType, Panel anchorPanel = null)
        {
            if (tabType == TabTypes.CustomView)
            {
                Debug.LogError("Can't show a custom view just by tab type, needs the uniquely identifying name (label)!");
                return null;
            }

            if (_storedPanels.TryGetValue(tabType, out Panel storedPanel) && storedPanel != null)
            {
                storedPanel.gameObject.SetActive(true);
                if (anchorPanel != null)
                {
                    PanelManager.Instance.AnchorPanel(storedPanel, anchorPanel, Direction.Right);
                }
                else
                {
                    PanelManager.Instance.AnchorPanel(storedPanel, storedPanel.Canvas, Direction.Right);
                }
                _storedPanels.Remove(tabType);
                return storedPanel[0];
            }

            TabDefinition tabDefinition = TabDefinitions.Find(x => x.TypeID == tabType);
            PanelTab panelTab = CreatePanelTab(tabDefinition);
            if (panelTab == null)
            {
                return null;
            }
            if (anchorPanel != null)
            {
                PanelManager.Instance.AnchorPanel(panelTab.Panel, anchorPanel, Direction.Right);
            }
            return panelTab;
        }

        public bool IsTabActive(TabTypes tabType)
        {
            if (_storedPanels.TryGetValue(tabType, out Panel storedPanel) && storedPanel != null)
            {
                return storedPanel.gameObject.activeSelf;
            }

            if (PanelNotificationCenter.TryGetTab(tabType.ToString(), out PanelTab tab))
            {
                return tab.Panel.gameObject.activeSelf;
            }

            return false;
        }

        private PanelTab CreatePanelTab(TabDefinition tabDefinition)
        {
            if (tabDefinition == null)
            {
                Debug.LogError("No tab definition provided when attempting to create a panel tab.");
                return null;
            }

            if (tabDefinition.Prefab == null)
            {
                Debug.LogError($"Tab definition '{tabDefinition.Label}' does not have a prefab assigned.");
                return null;
            }

            DynamicPanelsCanvas dynamicPanelsCanvas = Editor.Instance.UIController.DynamicPanelsCanvas;

            Transform parent = _closedTabsRoot != null ? _closedTabsRoot : dynamicPanelsCanvas?.RectTransform;
            RectTransform tabContentInstance = parent != null
                ? Instantiate(tabDefinition.Prefab, parent, false)
                : Instantiate(tabDefinition.Prefab);

            Panel panel = PanelUtils.CreatePanelFor(tabContentInstance, dynamicPanelsCanvas);
            if (panel == null)
            {
                Destroy(tabContentInstance.gameObject);
                return null;
            }
            PanelTab panelTab = panel[0];
            panelTab.Icon = tabDefinition.Icon;
            panelTab.Label = tabDefinition.Label;
            panelTab.ID = tabDefinition.TypeID.ToString();

            return panelTab;
        }
    }

}
