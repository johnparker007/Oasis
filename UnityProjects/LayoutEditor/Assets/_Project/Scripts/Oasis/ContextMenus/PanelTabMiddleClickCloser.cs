using UnityEngine;
using UnityEngine.EventSystems;
using DynamicPanels;

// Closes a tab when it is middle-clicked.
public sealed class PanelTabMiddleClickCloser : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            var tab = GetComponent<PanelTab>();
            if (tab)
            {
                tab.Destroy();
            }
        }
    }
}
