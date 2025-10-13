using System;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis;

namespace Oasis.LayoutEditor
{
    public class BaseViewQuadOverlay : MonoBehaviour
    {
        private const float kHandleSize = 16f;
        private const float kEdgeThickness = 2f;

        private static readonly Color kFillColor = new Color(1.0f, 1.0f, 0.0f, 0.0f);
        private static readonly Color kEdgeColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);
        private static readonly Color kHandleColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);

        private readonly Vector2[] _points = new Vector2[Enum.GetValues(typeof(ViewQuad.PointTypes)).Length];

        private View _view;
        private RectTransform _contentRect;
        private Zoom _zoom;
        private RectTransform _rectTransform;
        private ViewQuadFillGraphic _fillGraphic;
        private RectTransform[] _edgeRects;
        private BaseViewQuadHandle[] _handles;
        private Vector2 _lastContentSize = Vector2.zero;

        private float ZoomLevel => _zoom != null ? Mathf.Max(_zoom.ZoomLevel, 0.0001f) : 1f;

        internal RectTransform ContentRect => _contentRect;

        public void Initialize(View view, RectTransform contentRect, Zoom zoom)
        {
            _view = view;
            _contentRect = contentRect;
            _zoom = zoom;
            _rectTransform = GetComponent<RectTransform>();

            ConfigureRectTransform();
            CreateVisuals();
            CopyPointsFromView();
            RefreshVisuals();

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.AddListener(OnZoomLevelChanged);
                OnZoomLevelChanged(_zoom.ZoomLevel);
            }
        }

        private void ConfigureRectTransform()
        {
            _rectTransform.anchorMin = new Vector2(0f, 1f);
            _rectTransform.anchorMax = new Vector2(0f, 1f);
            _rectTransform.pivot = new Vector2(0f, 1f);
            _rectTransform.anchoredPosition = Vector2.zero;
            _lastContentSize = GetContentSize();
            _rectTransform.sizeDelta = _lastContentSize;
        }

        private void CreateVisuals()
        {
            _fillGraphic = CreateFillGraphic();
            _edgeRects = new RectTransform[_points.Length];
            _handles = new BaseViewQuadHandle[_points.Length];

            for (int i = 0; i < _points.Length; ++i)
            {
                _edgeRects[i] = CreateEdgeRect(i);
            }

            for (int i = 0; i < _points.Length; ++i)
            {
                _handles[i] = CreateHandle(i);
            }
        }

        private ViewQuadFillGraphic CreateFillGraphic()
        {
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(ViewQuadFillGraphic));
            fillObject.transform.SetParent(transform, false);

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 1f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 1f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = _lastContentSize;

            ViewQuadFillGraphic fillGraphic = fillObject.GetComponent<ViewQuadFillGraphic>();
            fillGraphic.color = kFillColor;
            fillGraphic.raycastTarget = false;

            return fillGraphic;
        }

        private RectTransform CreateEdgeRect(int index)
        {
            GameObject edgeObject = new GameObject($"Edge{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edgeObject.transform.SetParent(transform, false);

            RectTransform rect = edgeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);

            Image image = edgeObject.GetComponent<Image>();
            image.color = kEdgeColor;
            image.raycastTarget = false;

            return rect;
        }

        private BaseViewQuadHandle CreateHandle(int index)
        {
            GameObject handleObject = new GameObject($"Handle{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BaseViewQuadHandle));
            handleObject.transform.SetParent(transform, false);

            RectTransform rect = handleObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = handleObject.GetComponent<Image>();
            image.color = kHandleColor;
            image.raycastTarget = true;

            BaseViewQuadHandle handle = handleObject.GetComponent<BaseViewQuadHandle>();
            handle.Initialize(this, index);

            return handle;
        }

        private void OnDestroy()
        {
            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.RemoveListener(OnZoomLevelChanged);
            }
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);

            if (active)
            {
                CopyPointsFromView();
                RefreshVisuals();
            }
        }

        public bool TryGetLayoutPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 layoutPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_contentRect, screenPosition, eventCamera, out Vector2 localPoint))
            {
                layoutPoint = Vector2.zero;
                return false;
            }

            layoutPoint = LocalToLayout(localPoint);
            return true;
        }

        public Vector2 GetPoint(int index)
        {
            return _points[index];
        }

        internal void SetPointFromHandle(int index, Vector2 layoutPoint)
        {
            layoutPoint = ApplyConstraints(layoutPoint);

            if (_points[index] == layoutPoint)
            {
                return;
            }

            _points[index] = layoutPoint;
            _view.Data.ViewQuad.Points[index] = layoutPoint;
            Editor.Instance.Project.Layout.Dirty = true;
            _view.OnChanged?.Invoke();

            RefreshVisuals();
        }

        private Vector2 ApplyConstraints(Vector2 layoutPoint)
        {
            layoutPoint.x = Mathf.Round(layoutPoint.x);
            layoutPoint.y = Mathf.Round(layoutPoint.y);

            Vector2 size = GetContentSize();
            if (size.x > 0f)
            {
                layoutPoint.x = Mathf.Clamp(layoutPoint.x, 0f, size.x);
            }

            if (size.y > 0f)
            {
                layoutPoint.y = Mathf.Clamp(layoutPoint.y, 0f, size.y);
            }

            return layoutPoint;
        }

        private Vector2 LocalToLayout(Vector2 localPoint)
        {
            return new Vector2(localPoint.x, -localPoint.y);
        }

        private Vector2 LayoutToAnchored(Vector2 layoutPoint)
        {
            return new Vector2(layoutPoint.x, -layoutPoint.y);
        }

        private void CopyPointsFromView()
        {
            bool hasNonZeroPoint = false;
            for (int i = 0; i < _points.Length; ++i)
            {
                _points[i] = _view.Data.ViewQuad.Points[i];
                if (!Mathf.Approximately(_points[i].x, 0f) || !Mathf.Approximately(_points[i].y, 0f))
                {
                    hasNonZeroPoint = true;
                }
            }

            if (!hasNonZeroPoint)
            {
                Vector2 size = GetContentSize();
                if (size.x > 0f && size.y > 0f)
                {
                    _view.SetViewQuadRectangle(0f, 0f, size.y, size.x);

                    for (int i = 0; i < _points.Length; ++i)
                    {
                        _points[i] = _view.Data.ViewQuad.Points[i];
                    }
                }
            }
        }

        private void RefreshVisuals()
        {
            Vector2 size = GetContentSize();
            _lastContentSize = size;
            _rectTransform.sizeDelta = size;

            UpdateFill();
            UpdateEdges();
            UpdateHandles();
        }

        private void UpdateFill()
        {
            if (_fillGraphic == null)
            {
                return;
            }

            _fillGraphic.rectTransform.sizeDelta = _lastContentSize;
            _fillGraphic.SetPoints(_points);
        }

        private void UpdateEdges()
        {
            for (int i = 0; i < _edgeRects.Length; ++i)
            {
                RectTransform edge = _edgeRects[i];
                int nextIndex = (i + 1) % _points.Length;

                Vector2 start = LayoutToAnchored(_points[i]);
                Vector2 end = LayoutToAnchored(_points[nextIndex]);

                Vector2 delta = end - start;
                float length = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                edge.anchoredPosition = start;
                edge.sizeDelta = new Vector2(length, kEdgeThickness / ZoomLevel);
                edge.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        private void UpdateHandles()
        {
            float size = kHandleSize / ZoomLevel;

            for (int i = 0; i < _handles.Length; ++i)
            {
                RectTransform rect = _handles[i].RectTransform;
                rect.anchoredPosition = LayoutToAnchored(_points[i]);
                rect.sizeDelta = new Vector2(size, size);
            }
        }

        private void OnZoomLevelChanged(float zoom)
        {
            UpdateEdges();
            UpdateHandles();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            Vector2 size = GetContentSize();
            if (size != _lastContentSize)
            {
                RefreshVisuals();
            }
        }

        private Vector2 GetContentSize()
        {
            Rect rect = _contentRect.rect;
            return new Vector2(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
        }
    }
}
