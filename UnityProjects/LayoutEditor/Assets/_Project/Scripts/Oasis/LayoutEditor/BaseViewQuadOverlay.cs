using System;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis;

namespace Oasis.LayoutEditor
{
    public class BaseViewQuadOverlay : MonoBehaviour
    {
        private const float kHandleSize = 48f;
        private const float kEdgeThickness = 2f;
        private const float kHandleOutlineThickness = 2f;

        private const float kHandleHoverAlpha = 1f;
        private const float kHandleIdleAlpha = 0.25f;

        private static readonly Color kFillColor = new Color(1.0f, 1.0f, 0.0f, 0.0f);
        private static readonly Color kEdgeColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);
        private static readonly Color kHandleColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);

        private readonly Vector2[] _points = new Vector2[Enum.GetValues(typeof(ViewQuad.PointTypes)).Length];

        private View _view;
        private ViewQuad _viewQuad;
        private RectTransform _contentRect;
        private Zoom _zoom;
        private RectTransform _rectTransform;
        private ViewQuadFillGraphic _fillGraphic;
        private RectTransform[] _edgeRects;
        private ViewQuadHandleGraphic[] _handleGraphics;
        private BaseViewQuadHandle[] _handles;
        private Vector2 _lastContentSize = Vector2.zero;
        private int _selectedHandleIndex = -1;

        private float ZoomLevel => _zoom != null ? Mathf.Max(_zoom.ZoomLevel, 0.0001f) : 1f;

        internal RectTransform ContentRect => _contentRect;

        public event Action<BaseViewQuadOverlay, int> OnHandleSelected;
        public event Action<BaseViewQuadOverlay> OnHandleSelectionCleared;

        public View View => _view;

        public int SelectedHandleIndex => _selectedHandleIndex;

        public bool HasSelectedHandle => _selectedHandleIndex >= 0 && _selectedHandleIndex < _points.Length;

        public int PointCount => _points.Length;

        public ViewQuad ViewQuad => _viewQuad;

        public string ViewQuadName
        {
            get
            {
                return _viewQuad?.Name;
            }
            set
            {
                if (_viewQuad == null)
                {
                    return;
                }

                string newName = value ?? string.Empty;
                if (string.Equals(_viewQuad.Name, newName, StringComparison.Ordinal))
                {
                    return;
                }

                _viewQuad.Name = newName;
                LayoutObject layout = Editor.Instance != null ? Editor.Instance.Project?.Layout : null;
                if (layout != null)
                {
                    layout.Dirty = true;
                }
                _view.OnChanged?.Invoke();
                RefreshOverlayName();
            }
        }

        public void Initialize(View view, RectTransform contentRect, Zoom zoom, ViewQuad viewQuad)
        {
            _view = view;
            _contentRect = contentRect;
            _zoom = zoom;
            _viewQuad = viewQuad;
            _rectTransform = GetComponent<RectTransform>();

            ConfigureRectTransform();
            CreateVisuals();
            SynchronizeWithViewQuad(true);
            RefreshOverlayName();

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
            _handleGraphics = new ViewQuadHandleGraphic[_points.Length];
            _handles = new BaseViewQuadHandle[_points.Length];

            for (int i = 0; i < _points.Length; ++i)
            {
                _edgeRects[i] = CreateEdgeRect(i);
            }

            for (int i = 0; i < _points.Length; ++i)
            {
                _handles[i] = CreateHandle(i);
            }

            ResetAllHandleHoverStates();
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
            GameObject handleObject = new GameObject($"Handle{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(ViewQuadHandleGraphic), typeof(BaseViewQuadHandle));
            handleObject.transform.SetParent(transform, false);

            RectTransform rect = handleObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            ViewQuadHandleGraphic handleGraphic = handleObject.GetComponent<ViewQuadHandleGraphic>();
            handleGraphic.color = GetHandleColorWithAlpha(kHandleIdleAlpha);
            handleGraphic.raycastTarget = true;

            _handleGraphics[index] = handleGraphic;
            BaseViewQuadHandle handle = handleObject.GetComponent<BaseViewQuadHandle>();
            handle.Initialize(this, index);

            return handle;
        }

        private Color GetHandleColorWithAlpha(float alpha)
        {
            Color color = kHandleColor;
            color.a = alpha;
            return color;
        }

        private void ResetAllHandleHoverStates()
        {
            if (_handleGraphics == null)
            {
                return;
            }

            for (int i = 0; i < _handleGraphics.Length; ++i)
            {
                SetHandleHoverState(i, false);
            }
        }

        internal void SetHandleHoverState(int index, bool isHovering)
        {
            if (_handleGraphics == null || index < 0 || index >= _handleGraphics.Length)
            {
                return;
            }

            ViewQuadHandleGraphic handleGraphic = _handleGraphics[index];
            if (handleGraphic == null)
            {
                return;
            }

            float targetAlpha = isHovering ? kHandleHoverAlpha : kHandleIdleAlpha;
            handleGraphic.color = GetHandleColorWithAlpha(targetAlpha);
        }

        private void OnDestroy()
        {
            ClearHandleSelection();

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.RemoveListener(OnZoomLevelChanged);
            }
        }

        public void SetActive(bool active)
        {
            if (!active)
            {
                ClearHandleSelection();
            }

            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(active);

            if (active && !wasActive)
            {
                SynchronizeWithViewQuad(true);
            }
        }

        public void SetPoint(int index, Vector2 layoutPoint)
        {
            SetPointInternal(index, layoutPoint);
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
            SetPointInternal(index, layoutPoint);
        }

        internal void SelectHandle(int index)
        {
            if (index < 0 || index >= _points.Length)
            {
                return;
            }

            _view?.TrySetActiveViewQuad(_viewQuad);
            _selectedHandleIndex = index;
            OnHandleSelected?.Invoke(this, index);
        }

        internal void ClearHandleSelection()
        {
            if (!HasSelectedHandle)
            {
                _selectedHandleIndex = -1;
                return;
            }

            _selectedHandleIndex = -1;
            OnHandleSelectionCleared?.Invoke(this);
        }

        private void SetPointInternal(int index, Vector2 layoutPoint)
        {
            if (_viewQuad == null)
            {
                return;
            }

            if (index < 0 || index >= _points.Length)
            {
                return;
            }

            layoutPoint = ApplyConstraints(layoutPoint);

            if (_points[index] == layoutPoint)
            {
                return;
            }

            _points[index] = layoutPoint;
            _viewQuad.Points[index] = layoutPoint;
            LayoutObject layout = Editor.Instance != null ? Editor.Instance.Project?.Layout : null;
            if (layout != null)
            {
                layout.Dirty = true;
            }
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
            if (_viewQuad == null)
            {
                for (int i = 0; i < _points.Length; ++i)
                {
                    _points[i] = Vector2.zero;
                }

                return;
            }

            Vector2[] viewQuadPoints = _viewQuad.Points;
            if (viewQuadPoints == null || viewQuadPoints.Length != _points.Length)
            {
                viewQuadPoints = new Vector2[_points.Length];
                _viewQuad.Points = viewQuadPoints;
            }

            bool hasNonZeroPoint = false;
            for (int i = 0; i < _points.Length; ++i)
            {
                _points[i] = viewQuadPoints[i];
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
                    viewQuadPoints[(int)ViewQuad.PointTypes.TopLeft] = new Vector2(0f, 0f);
                    viewQuadPoints[(int)ViewQuad.PointTypes.TopRight] = new Vector2(size.x, 0f);
                    viewQuadPoints[(int)ViewQuad.PointTypes.BottomRight] = new Vector2(size.x, size.y);
                    viewQuadPoints[(int)ViewQuad.PointTypes.BottomLeft] = new Vector2(0f, size.y);

                    for (int i = 0; i < _points.Length; ++i)
                    {
                        _points[i] = viewQuadPoints[i];
                    }

                    LayoutObject layout = Editor.Instance != null ? Editor.Instance.Project?.Layout : null;
                    if (layout != null)
                    {
                        layout.Dirty = true;
                    }

                    _view?.OnChanged?.Invoke();
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

                ViewQuadHandleGraphic handleGraphic = _handles[i].GetComponent<ViewQuadHandleGraphic>();
                if (handleGraphic != null)
                {
                    handleGraphic.LineWidth = kHandleOutlineThickness / ZoomLevel;
                }
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

        internal void SynchronizeWithViewQuad(bool resetHandleHoverStates)
        {
            CopyPointsFromView();
            RefreshVisuals();

            if (resetHandleHoverStates)
            {
                ResetAllHandleHoverStates();
            }
        }

        internal void RefreshOverlayName()
        {
            string viewName = _view != null ? _view.Name : string.Empty;
            string quadName = _viewQuad != null ? _viewQuad.Name : string.Empty;
            string displayName = string.IsNullOrWhiteSpace(quadName) ? "Unnamed ViewQuad" : quadName;

            if (!string.IsNullOrWhiteSpace(viewName))
            {
                gameObject.name = $"{viewName} ViewQuad - {displayName}";
            }
            else
            {
                gameObject.name = $"ViewQuad - {displayName}";
            }
        }

        private Vector2 GetContentSize()
        {
            Rect rect = _contentRect.rect;
            return new Vector2(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
        }
    }
}
