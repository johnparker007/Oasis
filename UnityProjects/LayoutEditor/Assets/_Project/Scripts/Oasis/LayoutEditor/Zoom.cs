using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Oasis.LayoutEditor
{
    using UnityEngine;

    public class Zoom : MonoBehaviour
    {
        private const float kInitialZoomLevel = 100f;

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

        private float _zoomLevel = 0f;

        private void Start()
        {
            _zoomLevel = kInitialZoomLevel;
        }

        private void Update()
        {
            if((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.mouseScrollDelta.y != 0f)
            {
                if(Input.mouseScrollDelta.y < 0f)
                {
                    ZoomOut();
                }
                else
                {
                    ZoomIn();
                }
            }
        }

        private void ZoomOut()
        {
            ZoomLevel *= 0.5f;
        }

        private void ZoomIn()
        {
            ZoomLevel *= 2f;
        }
    }
}
