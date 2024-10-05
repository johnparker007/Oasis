using UnityEngine;
using UnityEngine.EventSystems;


namespace Oasis.UI.Selection
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class SelectionInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const PointerEventData.InputButton kLeftMouseButton = PointerEventData.InputButton.Left;

        [SerializeField] private SelectionManager _selectionManager;


        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == kLeftMouseButton)
            {
                _selectionManager.StartSelection(eventData.position);
            }
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            if (_selectionManager.IsSelecting)
            {
                _selectionManager.UpdateSelection(eventData.position);
            }
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == kLeftMouseButton)
            {
                if (_selectionManager.IsSelecting)
                {
                    _selectionManager.EndSelection();
                }
            }
        }
    }
}


