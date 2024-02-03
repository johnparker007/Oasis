using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent2D : EditorComponent
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

        protected override void Awake()
        {

        }

        public override void Initialise(Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            _rectTransform = GetComponent<RectTransform>();

            Position = component.RectInt.position;
            Size = component.RectInt.size;

            UpdateRectTransformPosition(_rectTransform);
        }

        protected void UpdateRectTransformPosition(RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = new Vector2(Position.x, -Position.y);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size.y);
        }
    }

}

