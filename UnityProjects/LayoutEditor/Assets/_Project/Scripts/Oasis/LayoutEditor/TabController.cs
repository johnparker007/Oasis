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

        public List<TabDefinition> TabDefinitions;


        public PanelTab ShowTab(TabTypes tabType)
        {
            if(tabType == TabTypes.CustomView)
            {
                Debug.LogError("Can't show a custom view just by tab type, needs the uniquely identifying name (label)!");
                return null;
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
