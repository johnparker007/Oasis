using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Oasis.Utility
{
    public class OasisScrollRect : ScrollRect
    {
        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnBeginDrag(eventData);
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnEndDrag(eventData);
            }
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                eventData.button = PointerEventData.InputButton.Left;
                base.OnDrag(eventData);
            }
        }
    }

}
