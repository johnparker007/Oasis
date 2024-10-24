using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Oasis.LayoutEditor
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class Zoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float InitialZoomLevel = 1f;
        public float MinimumZoomLevel = 0.125f;
        public float MaximumZoomLevel = 16f;


        public RectTransform EditorCanvasRectTransform;

        public UnityEvent<float> OnZoomLevelSet = new UnityEvent<float>();

        public float ZoomLevel
        {
            get
            {
                return _zoomLevel;
            }
            private set
            {
                _zoomLevel = value;
                EditorCanvasRectTransform.localScale = new Vector3(_zoomLevel, _zoomLevel, _zoomLevel);
                OnZoomLevelSet?.Invoke(_zoomLevel);
            }
        }

        public bool PointerEntered
        {
            get;
            private set;
        }

        private float _zoomLevel = 0f;

        private void Awake()
        {
            _zoomLevel = InitialZoomLevel;
        }

        private void Update()
        {
            if(!PointerEntered)
            {
                return;
            }

            if((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.mouseScrollDelta.y != 0f)
            {
                if(Input.mouseScrollDelta.y < 0f)
                {
                    ZoomLevel *= 0.5f;
                }
                else
                {
                    ZoomLevel *= 2f;
                }
                ZoomLevel = Mathf.Clamp(ZoomLevel, MinimumZoomLevel, MaximumZoomLevel);
            }
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered = true;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            PointerEntered = false;
        }

    }
}
