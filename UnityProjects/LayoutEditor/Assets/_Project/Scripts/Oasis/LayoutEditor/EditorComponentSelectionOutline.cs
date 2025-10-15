using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class EditorComponentSelectionOutline : MonoBehaviour
    {
        private const float kOutlineThickness = 2f;

        private RectTransform _outlineRect;
        private ViewQuadHandleGraphic _outlineGraphic;
        private Zoom _zoom;
        private bool _initialised;

        private void Awake()
        {
            EnsureInitialised();
        }

        private void OnEnable()
        {
            if (_outlineRect != null && _outlineRect.gameObject.activeSelf)
            {
                UpdateLineWidth();
            }
        }

        private void OnDestroy()
        {
            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.RemoveListener(OnZoomLevelChanged);
            }
        }

        public void SetSelected(bool selected)
        {
            EnsureInitialised();

            if (_outlineRect == null)
            {
                return;
            }

            if (selected)
            {
                _outlineRect.gameObject.SetActive(true);
                _outlineRect.SetAsLastSibling();
                UpdateLineWidth();
            }
            else
            {
                _outlineRect.gameObject.SetActive(false);
            }
        }

        private void EnsureInitialised()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;

            RectTransform targetRect = GetComponent<RectTransform>();
            if (targetRect == null)
            {
                return;
            }

            GameObject outlineObject = new GameObject("SelectionOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(ViewQuadHandleGraphic));
            outlineObject.transform.SetParent(transform, false);
            outlineObject.layer = gameObject.layer;

            _outlineRect = outlineObject.GetComponent<RectTransform>();
            _outlineRect.anchorMin = Vector2.zero;
            _outlineRect.anchorMax = Vector2.one;
            _outlineRect.offsetMin = Vector2.zero;
            _outlineRect.offsetMax = Vector2.zero;
            _outlineRect.pivot = targetRect.pivot;
            _outlineRect.localScale = Vector3.one;
            _outlineRect.gameObject.SetActive(false);

            _outlineGraphic = outlineObject.GetComponent<ViewQuadHandleGraphic>();
            _outlineGraphic.color = Color.white;
            _outlineGraphic.raycastTarget = false;

            _zoom = GetComponentInParent<Zoom>();
            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.AddListener(OnZoomLevelChanged);
            }

            UpdateLineWidth();
        }

        private void OnZoomLevelChanged(float zoomLevel)
        {
            if (_outlineRect != null && _outlineRect.gameObject.activeSelf)
            {
                UpdateLineWidth();
            }
        }

        private void UpdateLineWidth()
        {
            if (_outlineGraphic == null)
            {
                return;
            }

            float zoomLevel = _zoom != null ? Mathf.Max(_zoom.ZoomLevel, 0.0001f) : 1f;
            _outlineGraphic.LineWidth = kOutlineThickness / zoomLevel;
        }
    }
}

