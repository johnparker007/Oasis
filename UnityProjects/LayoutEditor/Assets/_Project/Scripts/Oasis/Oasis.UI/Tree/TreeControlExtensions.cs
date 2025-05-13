using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.UI.TreeView
{
    public static class TreeControlExtensions
    {
        //TODO: Hook into new input system.
        public static bool IsPointerValid(this PointerEventData eventData)
        {
            for (int i = UnityEngine.Input.touchCount - 1; i >= 0; i--)
            {
                if (UnityEngine.Input.GetTouch(i).fingerId == eventData.pointerId)
                {
                    return true;
                }
            }

            return UnityEngine.Input.GetMouseButton((int)eventData.button);
        }
    }
}
