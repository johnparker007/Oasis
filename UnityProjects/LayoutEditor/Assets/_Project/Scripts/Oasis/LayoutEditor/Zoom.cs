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

            if((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.mouseScrollDelta.y != 0f)
            {
                float scrollDelta = Input.mouseScrollDelta.y;
                float previousZoom = ZoomLevel;
                float step = Mathf.Max(0.0001f, _zoomStep);
                float zoomFactor = Mathf.Pow(1f + step, scrollDelta);
                float newZoom = Mathf.Clamp(previousZoom * zoomFactor, MinimumZoomLevel, MaximumZoomLevel);

                if(!Mathf.Approximately(newZoom, previousZoom))
                {
                    bool hasLocalPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(EditorCanvasRectTransform, Input.mousePosition, null, out Vector2 localPoint);
                    bool hasParentPoint = false;
                    Vector2 parentLocalPoint = Vector2.zero;
                    RectTransform parentRectTransform = EditorCanvasRectTransform.parent as RectTransform;

                    if(parentRectTransform != null)
                    {
                        hasParentPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRectTransform, Input.mousePosition, null, out parentLocalPoint);
                    }

                    ZoomLevel = newZoom;

                    if(hasLocalPoint && hasParentPoint && parentRectTransform != null)
                    {
                        Vector2 scaleSign = new Vector2(
                            Mathf.Sign(EditorCanvasRectTransform.lossyScale.x),
                            Mathf.Sign(EditorCanvasRectTransform.lossyScale.y));

                        if(scaleSign.x == 0f)
                        {
                            scaleSign.x = 1f;
                        }

                        if(scaleSign.y == 0f)
                        {
                            scaleSign.y = 1f;
                        }

                        Vector2 scaledLocalPoint = Vector2.Scale(localPoint * newZoom, scaleSign);
                        Vector2 targetLocalPosition = parentLocalPoint - scaledLocalPoint;

                        Vector2 parentSize = parentRectTransform.rect.size;
                        Vector2 parentPivotOffset = Vector2.Scale(parentSize, parentRectTransform.pivot);
                        Vector2 anchorAverage = Vector2.Scale(
                            parentSize,
                            (EditorCanvasRectTransform.anchorMin + EditorCanvasRectTransform.anchorMax) * 0.5f);
                        Vector2 anchorReferencePoint = anchorAverage - parentPivotOffset;

                        EditorCanvasRectTransform.anchoredPosition = targetLocalPosition - anchorReferencePoint;
                    }
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

    }
}
