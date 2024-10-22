using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
using System;

public class ViewController : MonoBehaviour
{
    [Serializable]
    public class PanelDefinition
    {
        public RectTransform RectTransform;
        public string Label;
        public Sprite Icon;
    }


    // TOIMPROVE if this approach works, should have ref to this in the Editor instance
    // also may benefit from a Controller class with a nice API
    public DynamicPanelsCanvas DynamicPanelsCanvas;

    public PanelDefinition BasePanelDefinition;
    public PanelDefinition MamePanelDefinition;


    public void AddViewMame()
    {
        PanelTab mamePanelTab = CreatePanelTab(MamePanelDefinition);
    }

    private PanelTab CreatePanelTab(PanelDefinition panelDefinition)
    {
        Panel panel = PanelUtils.CreatePanelFor(panelDefinition.RectTransform, DynamicPanelsCanvas);
        PanelTab panelTab = panel[0];
        panelTab.Icon = panelDefinition.Icon;
        panelTab.Label = panelDefinition.Label;

        return panelTab;
    }
}
