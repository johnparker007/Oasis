using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.UI
{
	public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		private const int kNonExistingTouch = -98456;

		private RectTransform _rectTransform = null;
		private int _pointerId = kNonExistingTouch;
		private Vector2 _initialTouchPosition = Vector2.zero;

		private void Awake()
		{
			_rectTransform = (RectTransform)transform;
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			if(_pointerId != kNonExistingTouch)
			{
				eventData.pointerDrag = null;
				return;
			}

			_pointerId = eventData.pointerId;

			RectTransformUtility.ScreenPointToLocalPointInRectangle( 
				_rectTransform, eventData.position, eventData.pressEventCamera, out _initialTouchPosition);
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (eventData.pointerId != _pointerId)
            {
				return;
			}

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 touchPosition);

            _rectTransform.anchoredPosition += touchPosition - _initialTouchPosition;
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if(eventData.pointerId != _pointerId)
            {
				return;
			}

			_pointerId = kNonExistingTouch;
		}
	}
}