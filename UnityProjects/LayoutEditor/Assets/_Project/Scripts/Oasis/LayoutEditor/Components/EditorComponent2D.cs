using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent2D : EditorComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Vector2Int Position
        {
            get;
            protected set;
        }

        public Vector2Int Size
        {
            get;
            protected set;
        }

        protected RectTransform _rectTransform = null;

        
        public override void Initialise(Layout.Component component)
        {
            _rectTransform = GetComponent<RectTransform>(); 
            
            base.Initialise(component);
        }

        protected override void Refresh()
        {
            base.Refresh();

            Position = Component.Position;
            Size = Component.Size;

            UpdateRectTransformPosition(_rectTransform);
        }

        protected void UpdateRectTransformPosition(RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = new Vector2(Position.x, -Position.y);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size.y);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
           // throw new System.NotImplementedException();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            //throw new System.NotImplementedException();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            //Debug.LogError("Editor component " + gameObject.name + " - OnPointerDown " + eventData.position);

            //throw new System.NotImplementedException();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            //throw new System.NotImplementedException();
        }
    }

}

