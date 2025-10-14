using Oasis.Layout;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    public class EditorView : MonoBehaviour, IPointerClickHandler
    {
        public string ViewName;

        public GraphicRaycaster Content;

        public UnityEvent<List<EditorComponent>> OnPointerClickEvent;

        private bool _initialised = false;
        private List<EditorComponent> _editorComponents = new List<EditorComponent>();
        private RectTransform _contentContainer = null;
        private Vector2 _lastKnownContentSize = Vector2.zero;
        private bool _hasCenteredContent = false;

        private RectTransform ContentRectTransform => Content != null ? Content.GetComponent<RectTransform>() : null;

        public View View
        {
            get
            {
                return Editor.Instance.Project.Layout.GetView(ViewName);
            }
        }

        public EditorViewContentLayer GetLayer(EditorViewContentLayer.LayerTypes layerType)
        {
            List<EditorViewContentLayer> layers = Content.GetComponentsInChildren<EditorViewContentLayer>(true).ToList();

            return layers.Find(x => x.LayerType == layerType);
        }

        public Transform GetLayerTransform(EditorViewContentLayer.LayerTypes layerType)
        {
            return GetLayer(layerType).transform;
        }

        private void Awake()
        {
            EnsureContentContainer();
        }

        private void OnEnable()
        {
            Editor.Instance.OnEditorViewEnabled?.Invoke(this);
        }

        private void OnDisable()
        {
            Editor.Instance.OnEditorViewDisabled?.Invoke(this);
        }

        private void OnDestroy()
        {
            if(_initialised)
            {
                Editor.Instance.Project.Layout.OnAddComponent.RemoveListener(OnLayoutAddComponent);
            }
        }

        public void Initialise()
        {
            if(_initialised)
            {
                return;
            }

            EnsureContentContainer();
            Editor.Instance.Project.Layout.OnAddComponent.AddListener(OnLayoutAddComponent);

            View layoutView = this.View;
            if (layoutView != null)
            {
                foreach (Layout.Component component in layoutView.Data.Components)
                {
                    OnLayoutAddComponent(component, layoutView);
                }
            }

            _initialised = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                List<RaycastResult> results = new List<RaycastResult>();
                Content.Raycast(eventData, results);

                List<EditorComponent> editorComponents = new List<EditorComponent>();
                foreach (RaycastResult result in results)
                {
                    EditorComponent editorComponent = result.gameObject.GetComponent<EditorComponent>();
                    if (editorComponent != null)
                    {
                        editorComponents.Add(editorComponent);
                    }
                }

                if (editorComponents.Count > 0)
                {
                    OnPointerClickEvent?.Invoke(editorComponents);
                }
            }
        }

        public EditorComponent GetEditorComponent(Layout.Component component)
        {
            return _editorComponents.Find(x => x.Component == component);
        }

        private void OnLayoutAddComponent(Layout.Component component, View view)
        {
            if(view.Name != ViewName)
            {
                return;
            }

            EditorComponent editorComponent = null;
            if (component.GetType() == typeof(ComponentBackground))
            {
                editorComponent = AddComponentBackground((ComponentBackground)component);
            }
            else if (component.GetType() == typeof(ComponentLamp))
            {
                editorComponent = AddComponentLamp((ComponentLamp)component);
            }
            else if (component.GetType() == typeof(ComponentReel))
            {
                editorComponent = AddComponentReel((ComponentReel)component);
            }
            else if (component.GetType() == typeof(Component7Segment))
            {
                editorComponent = AddComponent7Segment((Component7Segment)component);
            }
            else if (component.GetType() == typeof(ComponentAlpha))
            {
                editorComponent = AddComponentAlpha((ComponentAlpha)component);
            }

            _editorComponents.Add(editorComponent);
        }

        private EditorComponent AddComponentBackground(ComponentBackground component)
        {
            EditorComponentBackground editorComponentBackground = Instantiate(
                Editor.Instance.EditorComponentBackgroundPrefab,
                GetLayerTransform(EditorViewContentLayer.LayerTypes.Background));

            editorComponentBackground.Initialise(component);

            // JP quick hack for now:
            RectTransform editorCanvasRectTransform = Content.GetComponent<RectTransform>();
            editorCanvasRectTransform.sizeDelta = new Vector2(component.Size.x, component.Size.y);

            SyncContentContainerSize();

            return editorComponentBackground;
        }

        private EditorComponent AddComponentLamp(ComponentLamp component)
        {
            EditorComponentLamp editorComponentLamp = Instantiate(
                Editor.Instance.EditorComponentLampPrefab,
                GetLayerTransform(EditorViewContentLayer.LayerTypes.AboveBackground));

            editorComponentLamp.Initialise(component);

            return editorComponentLamp;
        }

        private EditorComponent AddComponentReel(ComponentReel component)
        {
            // TODO this can't work with how we currently do solid color layers, with no image:
            // will need to either use image to 'punch out' the alpha windows... or something else...
            EditorComponentReel editorComponentReel = Instantiate(
                Editor.Instance.EditorComponentReelPrefab,
                GetLayerTransform(EditorViewContentLayer.LayerTypes.BelowBackground));

            editorComponentReel.Initialise(component);

            return editorComponentReel;
        }

        private EditorComponent AddComponent7Segment(Component7Segment component)
        {
            // TOIMPROVE - this will ultimately want to be Below the background glass with
            // alpha windows punched out that can later be refined in the Layout Editor
            EditorComponent7Segment editorComponent7Segment = Instantiate(
                Editor.Instance.EditorComponentSevenSegmentPrefab,
                GetLayerTransform(EditorViewContentLayer.LayerTypes.AboveBackground));

            editorComponent7Segment.Initialise(component);

            return editorComponent7Segment;
        }

        private EditorComponent AddComponentAlpha(ComponentAlpha component)
        {
            // TODO kinda hacky for now, until decided how this is going to work wrt design:
            // TOIMPROVE - I think it's probvably better to have a single EditorComponentAlpha,
            // that can be set into one of the four 'modes'; 14, 14+semicolon, 16, 16+semecolon
            // and theoretically changed on the fly - more versatile

            // TOIMPROVE - this will ultimately want to be Below the background glass with
            // alpha windows punched out that can later be refined in the Layout Editor
            EditorComponent editorComponent;
            switch (Editor.Instance.Project.Settings.FruitMachine.Platform)
            {
                case MAME.MameController.PlatformType.Scorpion4:
                    EditorComponentAlpha14 editorComponentAlpha14 = Instantiate(
                        Editor.Instance.EditorComponentAlpha14Prefab,
                        GetLayerTransform(EditorViewContentLayer.LayerTypes.AboveBackground));

                    editorComponent = editorComponentAlpha14;

                    editorComponentAlpha14.Initialise(component);
                    break;
                case MAME.MameController.PlatformType.MPU4:
                default:
                    EditorComponentAlpha editorComponentAlpha16 = Instantiate(
                        Editor.Instance.EditorComponentAlphaPrefab,
                        GetLayerTransform(EditorViewContentLayer.LayerTypes.AboveBackground));

                    editorComponent = editorComponentAlpha16;

                    editorComponentAlpha16.Initialise(component);
                    break;
            }

            return editorComponent;
        }

        private void LateUpdate()
        {
            RectTransform contentRect = ContentRectTransform;
            if (contentRect == null)
            {
                return;
            }

            if (contentRect.hasChanged)
            {
                contentRect.hasChanged = false;
                SyncContentContainerSize();
                CenterContent();
            }
        }

        private RectTransform EnsureContentContainer()
        {
            if (_contentContainer != null)
            {
                return _contentContainer;
            }

            RectTransform contentRect = ContentRectTransform;
            if (contentRect == null)
            {
                return null;
            }

            RectTransform currentParent = contentRect.parent as RectTransform;
            if (currentParent == null)
            {
                return null;
            }

            EditorViewContentContainer existingContainer = currentParent.GetComponent<EditorViewContentContainer>();
            if (existingContainer != null)
            {
                _contentContainer = existingContainer.RectTransform;
                return _contentContainer;
            }

            GameObject containerObject = new GameObject("ContentContainer", typeof(RectTransform), typeof(EditorViewContentContainer));
            containerObject.layer = contentRect.gameObject.layer;

            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.SetParent(currentParent, false);
            containerRect.SetSiblingIndex(contentRect.GetSiblingIndex());
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.localScale = Vector3.one;

            contentRect.SetParent(containerRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            ScrollRect scrollRect = containerRect.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.content = containerRect;
            }

            Zoom zoom = GetComponentInParent<Zoom>();
            if (zoom != null)
            {
                RectTransform previousCanvas = zoom.EditorCanvasRectTransform;
                if (previousCanvas == null || previousCanvas == contentRect)
                {
                    zoom.EditorCanvasRectTransform = containerRect;
                }

                float zoomLevel = Mathf.Max(zoom.ZoomLevel, Mathf.Epsilon);
                containerRect.localScale = new Vector3(zoomLevel, zoomLevel, zoomLevel);
            }

            _contentContainer = containerRect;

            Vector2 size = contentRect.rect.size;
            if (Mathf.Approximately(size.x, 0f) && Mathf.Approximately(size.y, 0f))
            {
                size = contentRect.sizeDelta;
            }

            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

            _lastKnownContentSize = size;
            _hasCenteredContent = false;

            CenterContent();

            return _contentContainer;
        }

        private void SyncContentContainerSize()
        {
            RectTransform contentRect = ContentRectTransform;
            RectTransform containerRect = EnsureContentContainer();
            if (contentRect == null || containerRect == null)
            {
                return;
            }

            Vector2 size = contentRect.rect.size;
            if (Mathf.Approximately(size.x, 0f) && Mathf.Approximately(size.y, 0f))
            {
                size = contentRect.sizeDelta;
            }

            if (Approximately(size, _lastKnownContentSize))
            {
                return;
            }

            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

            _lastKnownContentSize = size;
            _hasCenteredContent = false;

            CenterContent();
        }

        private void CenterContent()
        {
            if (_hasCenteredContent)
            {
                return;
            }

            RectTransform containerRect = EnsureContentContainer();
            if (containerRect == null)
            {
                return;
            }

            ScrollRect scrollRect = containerRect.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);
            }

            containerRect.anchoredPosition = Vector2.zero;

            _hasCenteredContent = true;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }
    }

}

