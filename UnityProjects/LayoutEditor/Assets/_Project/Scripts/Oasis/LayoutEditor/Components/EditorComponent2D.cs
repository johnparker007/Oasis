using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent2D : EditorComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float kSelectionOutlineThickness = 2f;

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

        private ViewQuadHandleGraphic _selectionOutline = null;
        private RectTransform _selectionOutlineRect = null;
        private Zoom _zoom = null;

        protected override void Awake()
        {
            base.Awake();

            _rectTransform = GetComponent<RectTransform>();

            CreateSelectionOutline();
            SubscribeToSelectionEvents();
            SubscribeToZoom();
            UpdateSelectionOutlineVisibility();
        }

        protected virtual void OnEnable()
        {
            UpdateSelectionOutlineVisibility();
        }

        protected virtual void OnDisable()
        {
            if (_selectionOutlineRect != null)
            {
                _selectionOutlineRect.gameObject.SetActive(false);
            }
        }


        public override void Initialise(Layout.Component component)
        {
            _rectTransform = GetComponent<RectTransform>();

            base.Initialise(component);
        }

        protected override void OnDestroy()
        {
            if (Editor.Instance != null && Editor.Instance.SelectionController != null)
            {
                Editor.Instance.SelectionController.OnSelectionChange.RemoveListener(OnSelectionChanged);
            }

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.RemoveListener(OnZoomLevelChanged);
            }

            base.OnDestroy();
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

        private void CreateSelectionOutline()
        {
            if (_selectionOutline != null)
            {
                return;
            }

            GameObject outlineObject = new GameObject("SelectionOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(ViewQuadHandleGraphic));
            outlineObject.hideFlags = HideFlags.HideAndDontSave;
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.SetAsLastSibling();

            _selectionOutlineRect = outlineObject.GetComponent<RectTransform>();
            _selectionOutlineRect.anchorMin = Vector2.zero;
            _selectionOutlineRect.anchorMax = Vector2.one;
            _selectionOutlineRect.pivot = new Vector2(0.5f, 0.5f);
            _selectionOutlineRect.anchoredPosition = Vector2.zero;
            _selectionOutlineRect.sizeDelta = Vector2.zero;

            _selectionOutline = outlineObject.GetComponent<ViewQuadHandleGraphic>();
            _selectionOutline.color = Color.white;
            _selectionOutline.raycastTarget = false;
            _selectionOutline.LineWidth = kSelectionOutlineThickness;

            outlineObject.SetActive(false);
        }

        private void SubscribeToSelectionEvents()
        {
            if (Editor.Instance == null || Editor.Instance.SelectionController == null)
            {
                return;
            }

            Editor.Instance.SelectionController.OnSelectionChange.AddListener(OnSelectionChanged);
        }

        private void SubscribeToZoom()
        {
            EditorPanel editorPanel = GetComponentInParent<EditorPanel>();
            _zoom = editorPanel != null ? editorPanel.Zoom : null;

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.AddListener(OnZoomLevelChanged);
                OnZoomLevelChanged(_zoom.ZoomLevel);
            }
            else
            {
                OnZoomLevelChanged(1f);
            }
        }

        private void OnSelectionChanged()
        {
            UpdateSelectionOutlineVisibility();
        }

        private void UpdateSelectionOutlineVisibility()
        {
            if (_selectionOutlineRect == null)
            {
                return;
            }

            bool isSelected = Editor.Instance != null &&
                Editor.Instance.SelectionController != null &&
                Editor.Instance.SelectionController.SelectedEditorComponents.Contains(this);

            _selectionOutlineRect.gameObject.SetActive(isSelected);
        }

        private void OnZoomLevelChanged(float zoomLevel)
        {
            if (_selectionOutline == null)
            {
                return;
            }

            float level = Mathf.Max(zoomLevel, 0.0001f);
            _selectionOutline.LineWidth = kSelectionOutlineThickness / level;
        }
    }

}

