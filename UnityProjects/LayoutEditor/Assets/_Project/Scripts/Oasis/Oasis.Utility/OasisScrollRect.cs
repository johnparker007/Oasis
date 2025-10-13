using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Oasis.Utility
{
    public class OasisScrollRect : ScrollRect
    {
        public UnityEvent<PointerEventData> OnBeginDragEvent;
        public UnityEvent<PointerEventData> OnDragEvent;
        public UnityEvent<PointerEventData> OnEndDragEvent;


        public override void OnBeginDrag(PointerEventData eventData)
        {
            OnBeginDragEvent?.Invoke(eventData);

            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnBeginDrag(eventData);
            }
        }

        public override void OnDrag(PointerEventData eventData)
        {
            OnDragEvent?.Invoke(eventData);
            
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnDrag(eventData);
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            OnEndDragEvent?.Invoke(eventData);
            
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnEndDrag(eventData);
            }
        }

        public override void OnScroll(PointerEventData data)
        {
            if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                data.Use();
                return;
            }

            // JP fix for longstanding Unity bug where horizontal scrolling is reversed:
            Vector2 scrollDelta = data.scrollDelta;
            scrollDelta.x *= -1f;
            data.scrollDelta = scrollDelta;

            base.OnScroll(data);
        }

    }

}
