using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(RectTransform))]
    public class BaseViewQuadHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private BaseViewQuadOverlay _overlay;
        private int _index;
        private Vector2 _dragOffset;

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
    }
}
