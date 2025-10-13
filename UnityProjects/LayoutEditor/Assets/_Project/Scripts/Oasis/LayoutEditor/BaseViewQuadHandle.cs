using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(RectTransform))]
    public class BaseViewQuadHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private BaseViewQuadOverlay _overlay;
        private int _index;
        private Vector2 _dragOffset;
        private bool _isPointerInside = false;
        private int _activePointerId = -1;

        public RectTransform RectTransform
        {
            get;
            private set;
        }

        public void Initialize(BaseViewQuadOverlay overlay, int index)
        {
            _overlay = overlay;
            _index = index;
            RectTransform = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_overlay.TryGetLayoutPoint(eventData.position, eventData.pressEventCamera, out Vector2 layoutPoint))
            {
                _dragOffset = _overlay.GetPoint(_index) - layoutPoint;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_overlay.TryGetLayoutPoint(eventData.position, eventData.pressEventCamera, out Vector2 layoutPoint))
            {
                layoutPoint += _dragOffset;
                _overlay.SetPointFromHandle(_index, layoutPoint);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _overlay.NotifyHandleDragBegin();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _overlay.NotifyHandleDragEnd();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            _activePointerId = eventData.pointerId;
            _overlay.NotifyPointerEnter(eventData.pointerId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isPointerInside)
            {
                return;
            }

            _isPointerInside = false;
            _overlay.NotifyPointerExit(_activePointerId);
            _activePointerId = -1;
        }

        private void OnDisable()
        {
            if (_isPointerInside)
            {
                _isPointerInside = false;
                _overlay.NotifyPointerExit(_activePointerId);
                _activePointerId = -1;
            }

            _overlay.NotifyHandleDragEnd();
        }
    }
}
