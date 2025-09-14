using UnityEngine;
using UnityEngine.EventSystems;

// Toggles a tab's maximise/restore state when it is double-clicked.
public sealed class PanelTabDoubleClickMaximiser : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
        {
            var maximiser = GetComponent<PanelTabMaximiser>();
            if (maximiser)
            {
                maximiser.ToggleMaximise();
            }
        }
    }
}
