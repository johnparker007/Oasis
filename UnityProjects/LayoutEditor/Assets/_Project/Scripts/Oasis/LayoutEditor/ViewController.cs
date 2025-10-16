using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
using System;
using System.Collections;
using Oasis.Layout;
using System.Linq;
using System.Collections.Generic;

namespace Oasis.LayoutEditor
{
    public class ViewController : MonoBehaviour
    {
        public const string kBaseViewName = "Base";
        public const string kMameViewName = "Mame";

        private readonly Dictionary<ViewQuad, BaseViewQuadOverlay> _baseViewQuadOverlays = new();
        private Coroutine _ensureBaseViewQuadOverlayRoutine = null;
        private bool _listenersRegistered = false;
        private View _baseViewWithOverlays = null;

        private void OnEnable()
        {
            if (!TryRegisterListeners())
            {
                StartCoroutine(WaitForEditorAndInitialize());
                return;
            }

            EnsureBaseViewQuadOverlayVisible();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _ensureBaseViewQuadOverlayRoutine = null;
            UnregisterListeners();
            ResetBaseViewQuadOverlays();
        }

        private IEnumerator WaitForEditorAndInitialize()
        {
            yield return new WaitUntil(() => Editor.Instance != null);

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            if (TryRegisterListeners())
            {
                EnsureBaseViewQuadOverlayVisible();
            }
        }

        private bool TryRegisterListeners()
        {
            if (_listenersRegistered)
            {
                return true;
            }

            if (Editor.Instance == null)
            {
                return false;
            }

            Editor.Instance.OnLayoutSet.AddListener(OnLayoutSet);
            Editor.Instance.OnEditorViewEnabled.AddListener(OnEditorViewEnabled);
            Editor.Instance.OnEditorViewDisabled.AddListener(OnEditorViewDisabled);
            _listenersRegistered = true;
            return true;
        }

        private void UnregisterListeners()
        {
            if (!_listenersRegistered)
            {
                return;
            }

            if (Editor.Instance != null)
            {
                Editor.Instance.OnLayoutSet.RemoveListener(OnLayoutSet);
                Editor.Instance.OnEditorViewEnabled.RemoveListener(OnEditorViewEnabled);
                Editor.Instance.OnEditorViewDisabled.RemoveListener(OnEditorViewDisabled);
            }

            _listenersRegistered = false;
        }

        private void OnLayoutSet(Oasis.LayoutObject layout)
        {
            ResetBaseViewQuadOverlays();
            EnsureBaseViewQuadOverlayVisible();
        }

        private void OnEditorViewEnabled(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            if (string.Equals(editorView.ViewName, kBaseViewName, StringComparison.Ordinal))
            {
                EnsureBaseViewQuadOverlayVisible();
            }
        }

        private void OnEditorViewDisabled(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            if (string.Equals(editorView.ViewName, kBaseViewName, StringComparison.Ordinal))
            {
                ResetBaseViewQuadOverlays();
            }
        }

        private void EnsureBaseViewQuadOverlayVisible()
        {
            if (TryEnsureBaseViewQuadOverlayVisible())
            {
                return;
            }

            if (_ensureBaseViewQuadOverlayRoutine == null)
            {
                _ensureBaseViewQuadOverlayRoutine = StartCoroutine(WaitForBaseViewQuadOverlay());
            }
        }

        private bool TryEnsureBaseViewQuadOverlayVisible()
        {
            return TrySynchronizeBaseViewQuadOverlays();
        }

        private IEnumerator WaitForBaseViewQuadOverlay()
        {
            while (isActiveAndEnabled)
            {
                if (TryEnsureBaseViewQuadOverlayVisible())
                {
                    break;
                }

                yield return null;
            }

            _ensureBaseViewQuadOverlayRoutine = null;
        }

        public PanelTab ViewMamePanelTab
        {
            get;
            private set;
        } = null;

        public static EditorView GetEditorView(string viewName)
        {
            DynamicPanelsCanvas dynamicPanelsCanvas = Editor.Instance.UIController.DynamicPanelsCanvas;

            List<EditorView> editorViews = dynamicPanelsCanvas.GetComponentsInChildren<EditorView>(true).ToList();

            EditorView editorView = editorViews.Find(x => x.ViewName == viewName);

            return editorView;
        }

        public void AddViewMame()
        {
            ViewMamePanelTab = Editor.Instance.TabController.ShowTab(TabController.TabTypes.MameView);
            View viewMame = Editor.Instance.Project.Layout.AddView(kMameViewName);

            // for now initialise MAME view quad as the size of the Base view background
            // TOIMPROVE - potentially... can the Base view not have a background, or are we
            // always forcing that as a required component on a new Base view?
            View viewBase = Editor.Instance.Project.Layout.BaseView;

            ComponentBackground baseBackground =
                (ComponentBackground)viewBase.Data.Components.Find(x => x.GetType() == typeof(ComponentBackground));

            viewMame.SetViewQuadRectangle(
                baseBackground.Position.y,
                baseBackground.Position.x,
                baseBackground.Position.y + baseBackground.Size.y,
                baseBackground.Position.x + baseBackground.Size.x);
        }

        public void RebuildViewMame()
        {
            // TODO this is initial test code for now, just to attempt rebuild:

            View baseView = Editor.Instance.Project.Layout.GetView(kBaseViewName);
            View mameView = Editor.Instance.Project.Layout.GetView(kMameViewName);

            Debug.LogError("Before: baseView.Data.Components.Count == " + baseView.Data.Components.Count);
            Debug.LogError("Before: mameView.Data.Components.Count == " + mameView.Data.Components.Count);
            
            // TODO clear all mameView components and associated EditorComponents before rebuild
            ViewQuad mameViewQuad = mameView?.Data?.ViewQuad;
            if (mameViewQuad == null)
            {
                Debug.LogWarning("The MAME view does not have a ViewQuad configured; unable to rebuild.");
                return;
            }

            foreach(Layout.Component component in baseView.Data.Components)
            {
                // TOIMPROVE - this all wants refactoring into some kind of OasisRect system:
                if (!mameViewQuad.ContainsAnyPoint(
                    component.PointTopLeft,
                    component.PointTopRight,
                    component.PointBottomLeft,
                    component.PointBottomRight))
                {
                    continue;
                }

                Layout.Component componentClone = component.Clone();

                // TOIMPROVE put this in here for now, as not even sure if this is the best way to go about this:
                if(componentClone.Position.x < 0)
                {
                    componentClone.Size = new Vector2Int(componentClone.Size.x + componentClone.Position.x, componentClone.Size.y);
                    componentClone.Position = new Vector2Int(0, componentClone.Position.y);
                }

                if (componentClone.Position.y < 0)
                {
                    componentClone.Size = new Vector2Int(componentClone.Size.x, componentClone.Size.y + componentClone.Position.y);
                    componentClone.Position = new Vector2Int(componentClone.Position.x, 0);
                }

                mameView.AddComponent(componentClone);
            }

            // TODO target has more components than source! (prob reel overlays which
            // will be removed when background alpha baking is done)

            Debug.LogError("After: baseView.Data.Components.Count == " + baseView.Data.Components.Count);
            Debug.LogError("After: mameView.Data.Components.Count == " + mameView.Data.Components.Count);
        }

        private bool TrySynchronizeBaseViewQuadOverlays()
        {
            if (Editor.Instance == null || Editor.Instance.Project == null)
            {
                return false;
            }

            LayoutObject layout = Editor.Instance.Project.Layout;
            if (layout == null)
            {
                return false;
            }

            View baseView = layout.BaseView;
            if (baseView == null)
            {
                ResetBaseViewQuadOverlays();
                return false;
            }

            if ((baseView.ViewQuads == null || baseView.ViewQuads.Count == 0) && baseView.Data.ViewQuad != null)
            {
                baseView.EnsureViewQuad();
            }

            IReadOnlyList<ViewQuad> viewQuads = baseView.ViewQuads;
            if (viewQuads == null || viewQuads.Count == 0)
            {
                ResetBaseViewQuadOverlays();
                SubscribeToBaseView(null);
                return false;
            }

            EditorView editorView = baseView.EditorView;
            if (editorView == null || editorView.Content == null)
            {
                return false;
            }

            RectTransform contentRect = editorView.Content.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                return false;
            }

            EditorPanel editorPanel = editorView.GetComponentInParent<EditorPanel>();
            Zoom zoom = editorPanel != null ? editorPanel.Zoom : null;

            SubscribeToBaseView(baseView);

            InspectorController inspectorController = Editor.Instance.InspectorController;

            HashSet<ViewQuad> processed = new HashSet<ViewQuad>();
            foreach (ViewQuad viewQuad in viewQuads)
            {
                if (viewQuad == null)
                {
                    continue;
                }

                processed.Add(viewQuad);

                bool overlayCreated = false;
                if (!_baseViewQuadOverlays.TryGetValue(viewQuad, out BaseViewQuadOverlay overlay) || overlay == null)
                {
                    overlay = CreateBaseViewQuadOverlay(contentRect, baseView, viewQuad, zoom);
                    _baseViewQuadOverlays[viewQuad] = overlay;
                    inspectorController?.RegisterViewQuadOverlay(overlay);
                    overlayCreated = true;
                }

                if (overlay == null)
                {
                    continue;
                }

                if (overlayCreated)
                {
                    overlay.SetActive(true);
                }
                else if (!overlay.gameObject.activeSelf)
                {
                    overlay.SetActive(true);
                }
                else
                {
                    overlay.SynchronizeWithViewQuad(false);
                }

                overlay.RefreshOverlayName();

                if (ReferenceEquals(baseView.ActiveViewQuad, viewQuad))
                {
                    overlay.transform.SetAsLastSibling();
                }
            }

            if (_baseViewQuadOverlays.Count > processed.Count)
            {
                List<ViewQuad> toRemove = new List<ViewQuad>();
                foreach (KeyValuePair<ViewQuad, BaseViewQuadOverlay> pair in _baseViewQuadOverlays)
                {
                    if (!processed.Contains(pair.Key))
                    {
                        toRemove.Add(pair.Key);
                    }
                }

                foreach (ViewQuad removed in toRemove)
                {
                    if (_baseViewQuadOverlays.TryGetValue(removed, out BaseViewQuadOverlay overlay))
                    {
                        inspectorController?.UnregisterViewQuadOverlay(overlay);
                        if (overlay != null)
                        {
                            Destroy(overlay.gameObject);
                        }
                    }

                    _baseViewQuadOverlays.Remove(removed);
                }
            }

            return _baseViewQuadOverlays.Count > 0;
        }

        private BaseViewQuadOverlay CreateBaseViewQuadOverlay(RectTransform parent, View baseView, ViewQuad viewQuad, Zoom zoom)
        {
            GameObject overlayObject = new GameObject("BaseViewQuadOverlay", typeof(RectTransform), typeof(BaseViewQuadOverlay));
            overlayObject.transform.SetParent(parent, false);
            overlayObject.transform.SetAsLastSibling();

            BaseViewQuadOverlay overlay = overlayObject.GetComponent<BaseViewQuadOverlay>();
            overlay.Initialize(baseView, parent, zoom, viewQuad);
            return overlay;
        }

        private void ResetBaseViewQuadOverlays()
        {
            if (_ensureBaseViewQuadOverlayRoutine != null)
            {
                StopCoroutine(_ensureBaseViewQuadOverlayRoutine);
                _ensureBaseViewQuadOverlayRoutine = null;
            }

            SubscribeToBaseView(null);

            if (_baseViewQuadOverlays.Count == 0)
            {
                return;
            }

            InspectorController inspectorController = Editor.Instance != null ? Editor.Instance.InspectorController : null;

            foreach (BaseViewQuadOverlay overlay in _baseViewQuadOverlays.Values)
            {
                if (overlay == null)
                {
                    continue;
                }

                inspectorController?.UnregisterViewQuadOverlay(overlay);
                Destroy(overlay.gameObject);
            }

            _baseViewQuadOverlays.Clear();
        }

        private void SubscribeToBaseView(View baseView)
        {
            if (ReferenceEquals(_baseViewWithOverlays, baseView))
            {
                return;
            }

            if (_baseViewWithOverlays != null)
            {
                _baseViewWithOverlays.OnChanged.RemoveListener(OnBaseViewChanged);
            }

            _baseViewWithOverlays = baseView;

            if (_baseViewWithOverlays != null)
            {
                _baseViewWithOverlays.OnChanged.AddListener(OnBaseViewChanged);
            }
        }

        private void OnBaseViewChanged()
        {
            EnsureBaseViewQuadOverlayVisible();
        }


    }

}
