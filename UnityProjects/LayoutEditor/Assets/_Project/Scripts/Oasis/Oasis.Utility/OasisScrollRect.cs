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

    }

}
