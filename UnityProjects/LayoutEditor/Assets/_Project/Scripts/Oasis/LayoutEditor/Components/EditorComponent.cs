using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent : MonoBehaviour
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

        public Editor LayoutEditor
        {
            get;
            private set;
        }

        public virtual void Initialise(Layout.Component component, Editor layoutEditor)
        {
            Position = component.RectInt.position;
            Size = component.RectInt.size;

            LayoutEditor = layoutEditor;
        }

        public void UpdateRectTransformPosition(RectTransform rectTransform)
        {
            Canvas rootCanvas = LayoutEditor.UIController.RootCanvas;

            rectTransform.SetLocalPositionAndRotation(
                new Vector3(
                    Position.x - rootCanvas.transform.position.x, 
                    -Position.y + rootCanvas.transform.position.y, 0f),
                Quaternion.identity);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size.y);
        }
    }

}

