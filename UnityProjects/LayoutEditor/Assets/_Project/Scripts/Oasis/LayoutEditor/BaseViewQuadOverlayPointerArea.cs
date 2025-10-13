using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    public class BaseViewQuadOverlayPointerArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private BaseViewQuadOverlay _overlay;
        private bool _isPointerInside = false;
        private int? _activePointerId = null;

        public void Initialize(BaseViewQuadOverlay overlay)
        {
            _overlay = overlay;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_overlay == null || _isPointerInside)
            {
                return;
            }

            _isPointerInside = true;
            _activePointerId = eventData.pointerId;
            _overlay.NotifyPointerEnter(eventData.pointerId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_overlay == null || !_isPointerInside)
            {
                return;
            }

            _isPointerInside = false;
            if (_activePointerId.HasValue)
            {
                _overlay.NotifyPointerExit(_activePointerId.Value);
                _activePointerId = null;
            }
        }

        private void OnDisable()
        {
            if (_overlay != null && _isPointerInside && _activePointerId.HasValue)
            {
                _isPointerInside = false;
                _overlay.NotifyPointerExit(_activePointerId.Value);
            }

            _activePointerId = null;
        }
    }
}
