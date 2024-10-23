using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
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
            LampRemapper
        }

        [Serializable]
        public class TabDefinition
        {
            public string Label;
            public TabTypes TabType;
            public Sprite Icon;
            public RectTransform RectTransform;
        }

        public DynamicPanelsCanvas DynamicPanelsCanvas;

        public List<TabDefinition> TabDefinitions;


        public PanelTab ShowTab(TabTypes tabType)
        {
            if(tabType == TabTypes.CustomView)
            {
                Debug.LogError("Can't show a custom view just by tab type, needs the uniquely identifying name (label)!");
                return null;
            }

            TabDefinition tabDefinition = TabDefinitions.Find(x => x.TabType == tabType);
            PanelTab panelTab = CreatePanelTab(tabDefinition);
            return panelTab;
        }

        private PanelTab CreatePanelTab(TabDefinition tabDefinition)
        {
            Panel panel = PanelUtils.CreatePanelFor(tabDefinition.RectTransform, DynamicPanelsCanvas);
            PanelTab panelTab = panel[0];
            panelTab.Icon = tabDefinition.Icon;
            panelTab.Label = tabDefinition.Label;

            return panelTab;
        }
    }

}
