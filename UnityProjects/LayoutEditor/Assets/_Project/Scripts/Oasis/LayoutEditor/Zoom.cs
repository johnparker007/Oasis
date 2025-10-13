using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Oasis.LayoutEditor
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class Zoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        public float InitialZoomLevel = 1f;
        public float MinimumZoomLevel = 0.125f;
        public float MaximumZoomLevel = 16f;
        [SerializeField]
        [Tooltip("Fractional amount applied to each zoom step (e.g. 0.1 = 10% change per scroll).")]
        private float _zoomStep = 0.1f;


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

            if((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                float scrollDelta = Input.mouseScrollDelta.y;

                if(Mathf.Approximately(scrollDelta, 0f))
                {
                    return;
                }

                float previousZoom = ZoomLevel;
                float step = Mathf.Max(0.0001f, _zoomStep);
                float zoomFactor = Mathf.Pow(1f + step, scrollDelta);
                float newZoom = Mathf.Clamp(previousZoom * zoomFactor, MinimumZoomLevel, MaximumZoomLevel);

                if(!Mathf.Approximately(newZoom, previousZoom))
                {
                    ZoomLevel = newZoom;
                }
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

        void IScrollHandler.OnScroll(PointerEventData eventData)
        {
            if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                eventData.Use();
            }
        }

    }
}
