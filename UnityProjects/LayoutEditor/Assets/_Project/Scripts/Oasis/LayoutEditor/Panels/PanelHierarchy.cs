using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Oasis.Layout;
using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.Events;
using Component = Oasis.Layout.Component;
using View = Oasis.Layout.View;
using BaseViewQuadOverlay = Oasis.LayoutEditor.BaseViewQuadOverlay;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;
using SelectionController = Oasis.LayoutEditor.SelectionController;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelHierarchy : MonoBehaviour
    {
        private static readonly string[] s_defaultCategoryNames =
        {
            "Backgrounds",
            "Reels",
            "Alphas",
            "7 Segments",
            "Lamps"
        };

        private const string kFallbackCategoryName = "Components";
        private const string kViewQuadCategoryName = "View Quads";

        [SerializeField]
        private RuntimeHierarchy _runtimeHierarchy = null;

        private RuntimeHierarchyStandaloneTransformCollection _transformCollection;
        private readonly Dictionary<string, Transform> _categoryRoots = new(StringComparer.Ordinal);
        private readonly Dictionary<Component, ComponentEntry> _componentEntries = new();
        private readonly Dictionary<View, ViewQuadEntry> _viewQuadEntries = new();
        private readonly Dictionary<Transform, Component> _transformToComponent = new();
        private readonly Dictionary<Transform, View> _transformToViewQuad = new();
        private readonly Dictionary<View, BaseViewQuadOverlay> _viewToOverlay = new();

        private LayoutObject _observedLayout;
        private View _currentView;
        private string _currentViewName;
        private EditorView _currentEditorView;
        private bool _eventsSubscribed;
        private bool _runtimeHierarchyEventsSubscribed;
        private bool _ignoreEditorSelectionChange;
        private bool _ignoreHierarchySelectionChange;
        private bool _ignoreViewQuadSelectionEvents;
        private View _selectedViewQuad;

        private void Awake()
        {
            EnsureRuntimeHierarchy();
        }

        private void OnEnable()
        {
            EnsureRuntimeHierarchy();
            EnsureCategoryRoots();
            RefreshKnownOverlays();

            if (string.IsNullOrEmpty(_currentViewName))
            {
                _currentViewName = ViewController.kBaseViewName;
            }

            SubscribeToEditorEvents();
            SubscribeToLayout(Editor.Instance != null ? Editor.Instance.Project?.Layout : null);

            RefreshActiveView();
        }

        private void OnDisable()
        {
            UnsubscribeFromLayout();
            UnsubscribeFromEditorEvents();
            UnsubscribeFromRuntimeHierarchy();

            ClearComponentEntries();
            ClearViewQuadEntries();
            DestroyCategoryRoots();

            _viewToOverlay.Clear();
            _selectedViewQuad = null;
            _currentView = null;
            _currentEditorView = null;
            _currentViewName = null;
        }

        private void EnsureRuntimeHierarchy()
        {
            if (_runtimeHierarchy == null)
            {
                _runtimeHierarchy = GetComponentInChildren<RuntimeHierarchy>(true);
            }

            if (_runtimeHierarchy != null && _transformCollection == null)
            {
                _transformCollection = new RuntimeHierarchyStandaloneTransformCollection(_runtimeHierarchy);
            }

            SubscribeToRuntimeHierarchy();
        }

        private void SubscribeToRuntimeHierarchy()
        {
            if (_runtimeHierarchy == null || _runtimeHierarchyEventsSubscribed)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchyEventsSubscribed = true;
        }

        private void UnsubscribeFromRuntimeHierarchy()
        {
            if (!_runtimeHierarchyEventsSubscribed || _runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchyEventsSubscribed = false;
        }

        private void RefreshKnownOverlays()
        {
            _viewToOverlay.Clear();

            BaseViewQuadOverlay[] overlays = Resources.FindObjectsOfTypeAll<BaseViewQuadOverlay>();
            if (overlays == null)
            {
                return;
            }

            for (int i = 0; i < overlays.Length; i++)
            {
                BaseViewQuadOverlay overlay = overlays[i];
                if (overlay == null || overlay.View == null)
                {
                    continue;
                }

                _viewToOverlay[overlay.View] = overlay;
            }
        }

        private void OnViewQuadOverlayRegistered(BaseViewQuadOverlay overlay)
        {
            if (overlay == null || overlay.View == null)
            {
                return;
            }

            _viewToOverlay[overlay.View] = overlay;
        }

        private void OnViewQuadOverlayUnregistered(BaseViewQuadOverlay overlay)
        {
            if (overlay == null || overlay.View == null)
            {
                return;
            }

            if (_viewToOverlay.TryGetValue(overlay.View, out BaseViewQuadOverlay registered) && ReferenceEquals(registered, overlay))
            {
                _viewToOverlay.Remove(overlay.View);
            }
        }

        private void SubscribeToEditorEvents()
        {
            if (_eventsSubscribed)
            {
                return;
            }

            if (Editor.Instance == null)
            {
                return;
            }

            Editor.Instance.OnEditorViewEnabled.AddListener(OnEditorViewEnabled);
            Editor.Instance.OnEditorViewDisabled.AddListener(OnEditorViewDisabled);
            Editor.Instance.OnLayoutSet.AddListener(OnLayoutSet);

            if (Editor.Instance.SelectionController != null)
            {
                Editor.Instance.SelectionController.OnSelectionChange.AddListener(OnEditorSelectionChanged);
            }

            if (Editor.Instance.InspectorController != null)
            {
                InspectorController inspector = Editor.Instance.InspectorController;
                inspector.OnViewQuadOverlayRegistered += OnViewQuadOverlayRegistered;
                inspector.OnViewQuadOverlayUnregistered += OnViewQuadOverlayUnregistered;
                inspector.OnViewQuadHandleSelectedEvent += OnViewQuadHandleSelected;
                inspector.OnViewQuadHandleSelectionClearedEvent += OnViewQuadHandleSelectionCleared;
            }

            _eventsSubscribed = true;
        }

        private void UnsubscribeFromEditorEvents()
        {
            if (!_eventsSubscribed || Editor.Instance == null)
            {
                return;
            }

            Editor.Instance.OnEditorViewEnabled.RemoveListener(OnEditorViewEnabled);
            Editor.Instance.OnEditorViewDisabled.RemoveListener(OnEditorViewDisabled);
            Editor.Instance.OnLayoutSet.RemoveListener(OnLayoutSet);

            if (Editor.Instance.SelectionController != null)
            {
                Editor.Instance.SelectionController.OnSelectionChange.RemoveListener(OnEditorSelectionChanged);
            }

            if (Editor.Instance.InspectorController != null)
            {
                InspectorController inspector = Editor.Instance.InspectorController;
                inspector.OnViewQuadOverlayRegistered -= OnViewQuadOverlayRegistered;
                inspector.OnViewQuadOverlayUnregistered -= OnViewQuadOverlayUnregistered;
                inspector.OnViewQuadHandleSelectedEvent -= OnViewQuadHandleSelected;
                inspector.OnViewQuadHandleSelectionClearedEvent -= OnViewQuadHandleSelectionCleared;
            }

            _eventsSubscribed = false;
        }

        private void SubscribeToLayout(LayoutObject layout)
        {
            if (_observedLayout == layout)
            {
                return;
            }

            UnsubscribeFromLayout();

            if (layout == null)
            {
                return;
            }

            layout.OnAddComponent.AddListener(OnLayoutComponentAdded);
            layout.OnRemoveComponent.AddListener(OnLayoutComponentRemoved);

            _observedLayout = layout;
        }

        private void UnsubscribeFromLayout()
        {
            if (_observedLayout == null)
            {
                return;
            }

            _observedLayout.OnAddComponent.RemoveListener(OnLayoutComponentAdded);
            _observedLayout.OnRemoveComponent.RemoveListener(OnLayoutComponentRemoved);
            _observedLayout = null;
        }

        private void OnLayoutSet(LayoutObject layout)
        {
            SubscribeToLayout(layout);
            RefreshActiveView();
        }

        private void OnEditorViewEnabled(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            _currentEditorView = editorView;
            _currentViewName = editorView.ViewName;
            SetCurrentView(ResolveView(editorView));
        }

        private void OnEditorViewDisabled(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            if (ReferenceEquals(_currentEditorView, editorView))
            {
                _currentEditorView = null;
                SelectFallbackView();
            }
        }

        private void RefreshActiveView()
        {
            if (Editor.Instance == null)
            {
                SetCurrentView(null);
                return;
            }

            if (_currentEditorView != null && _currentEditorView.isActiveAndEnabled)
            {
                SetCurrentView(ResolveView(_currentEditorView));
                return;
            }

            EditorView activeEditorView = FindActiveEditorView();
            if (activeEditorView != null)
            {
                _currentEditorView = activeEditorView;
                SetCurrentView(ResolveView(activeEditorView));
                return;
            }

            SelectFallbackView();
        }

        private void SelectFallbackView()
        {
            if (Editor.Instance == null)
            {
                _currentViewName = ViewController.kBaseViewName;
                SetCurrentView(null);
                return;
            }

            EditorView baseEditorView = ViewController.GetEditorView(ViewController.kBaseViewName);
            if (baseEditorView != null && baseEditorView.isActiveAndEnabled)
            {
                _currentEditorView = baseEditorView;
                _currentViewName = baseEditorView.ViewName;
                SetCurrentView(ResolveView(baseEditorView));
                return;
            }

            View fallbackView = Editor.Instance.Project?.Layout?.BaseView;
            if (fallbackView != null)
            {
                _currentViewName = fallbackView.Name;
            }
            else
            {
                _currentViewName = ViewController.kBaseViewName;
            }
            SetCurrentView(fallbackView);
        }

        private static View ResolveView(EditorView editorView)
        {
            if (editorView == null)
            {
                return null;
            }

            return Editor.Instance?.Project?.Layout?.GetView(editorView.ViewName);
        }

        private EditorView FindActiveEditorView()
        {
            if (Editor.Instance?.UIController?.DynamicPanelsCanvas == null)
            {
                return null;
            }

            return Editor.Instance.UIController.DynamicPanelsCanvas
                .GetComponentsInChildren<EditorView>(true)
                .FirstOrDefault(view => view != null && view.isActiveAndEnabled);
        }

        private void SetCurrentView(View view)
        {
            if (!ReferenceEquals(_currentView, view))
            {
                _currentView = view;
            }

            if (view != null)
            {
                _currentViewName = view.Name;
            }

            RebuildHierarchyEntries();
        }

        private void RebuildHierarchyEntries()
        {
            EnsureRuntimeHierarchy();

            if (_runtimeHierarchy == null)
            {
                ClearComponentEntries();
                ClearViewQuadEntries();
                return;
            }

            _runtimeHierarchy.Deselect();

            ClearComponentEntries();
            ClearViewQuadEntries();
            EnsureCategoryRoots();

            BuildViewQuadEntries();

            if (_selectedViewQuad != null && !_viewQuadEntries.ContainsKey(_selectedViewQuad))
            {
                _selectedViewQuad = null;
            }

            View activeView = ResolveCurrentView();

            if (activeView == null)
            {
                _runtimeHierarchy.Refresh();
                UpdateHierarchySelectionFromEditor();
                return;
            }

            foreach (Component component in activeView.Data.Components)
            {
                AddComponentEntry(component);
            }

            _runtimeHierarchy.Refresh();
            UpdateHierarchySelectionFromEditor();
        }

        private void OnEditorSelectionChanged()
        {
            if (_ignoreEditorSelectionChange)
            {
                return;
            }

            UpdateHierarchySelectionFromEditor();
        }

        private void UpdateHierarchySelectionFromEditor()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            List<Transform> selectedTransforms = new List<Transform>();

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController != null)
            {
                List<EditorComponent> selectedComponents = selectionController.SelectedEditorComponents;
                for (int i = 0; i < selectedComponents.Count; i++)
                {
                    EditorComponent editorComponent = selectedComponents[i];
                    if (editorComponent?.Component == null)
                    {
                        continue;
                    }

                    if (_componentEntries.TryGetValue(editorComponent.Component, out ComponentEntry entry) && entry.Transform != null)
                    {
                        selectedTransforms.Add(entry.Transform);
                    }
                }
            }

            _ignoreHierarchySelectionChange = true;
            try
            {
                if (selectedTransforms.Count > 0)
                {
                    _selectedViewQuad = null;
                    _runtimeHierarchy.Select(selectedTransforms, RuntimeHierarchy.SelectOptions.FocusOnSelection | RuntimeHierarchy.SelectOptions.ForceRevealSelection);
                }
                else if (_selectedViewQuad != null && _viewQuadEntries.TryGetValue(_selectedViewQuad, out ViewQuadEntry entry) && entry.Transform != null)
                {
                    _runtimeHierarchy.Select(entry.Transform, RuntimeHierarchy.SelectOptions.FocusOnSelection | RuntimeHierarchy.SelectOptions.ForceRevealSelection);
                }
                else
                {
                    _runtimeHierarchy.Deselect();
                }
            }
            finally
            {
                _ignoreHierarchySelectionChange = false;
            }
        }

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            if (_ignoreHierarchySelectionChange)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController == null)
            {
                return;
            }

            _ignoreEditorSelectionChange = true;
            try
            {
                selectionController.DeselectAllObjects();

                bool hasComponentSelection = false;
                View selectedView = null;

                if (selection != null)
                {
                    for (int i = 0; i < selection.Count; i++)
                    {
                        Transform transform = selection[i];
                        if (transform == null)
                        {
                            continue;
                        }

                        if (_transformToComponent.TryGetValue(transform, out Component component))
                        {
                            EditorComponent editorComponent = FindEditorComponent(component);
                            if (editorComponent != null)
                            {
                                selectionController.SelectObject(editorComponent);
                                hasComponentSelection = true;
                            }
                        }
                        else if (_transformToViewQuad.TryGetValue(transform, out View view))
                        {
                            selectedView = view;
                        }
                    }
                }

                if (selectedView != null)
                {
                    _selectedViewQuad = selectedView;
                    UpdateHierarchySelectionFromEditor();
                    FocusViewQuad(selectedView);
                }
                else if (!hasComponentSelection)
                {
                    _selectedViewQuad = null;
                    UpdateHierarchySelectionFromEditor();
                }
                else
                {
                    _selectedViewQuad = null;
                }
            }
            finally
            {
                _ignoreEditorSelectionChange = false;
            }
        }

        private View ResolveCurrentView()
        {
            if (_currentView != null)
            {
                return _currentView;
            }

            if (string.IsNullOrEmpty(_currentViewName))
            {
                return null;
            }

            View resolvedView = Editor.Instance?.Project?.Layout?.GetView(_currentViewName);
            if (resolvedView != null)
            {
                _currentView = resolvedView;
                _currentViewName = resolvedView.Name;
            }

            return _currentView;
        }

        private void EnsureCategoryRoots()
        {
            if (_transformCollection == null)
            {
                return;
            }

            for (int i = 0; i < s_defaultCategoryNames.Length; i++)
            {
                GetOrCreateCategoryTransform(s_defaultCategoryNames[i]);
            }
        }

        private Transform GetOrCreateCategoryTransform(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                categoryName = kFallbackCategoryName;
            }

            if (_categoryRoots.TryGetValue(categoryName, out Transform existingTransform) && existingTransform != null)
            {
                return existingTransform;
            }

            Transform categoryTransform = CreateCategoryTransform(categoryName);
            _categoryRoots[categoryName] = categoryTransform;
            _transformCollection?.Add(categoryTransform);

            return categoryTransform;
        }

        private static Transform CreateCategoryTransform(string categoryName)
        {
            GameObject categoryObject = new GameObject(categoryName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            return categoryObject.transform;
        }

        private void DestroyCategoryRoots()
        {
            if (_categoryRoots.Count == 0)
            {
                return;
            }

            foreach (Transform categoryTransform in _categoryRoots.Values)
            {
                if (categoryTransform != null)
                {
                    _transformCollection?.Remove(categoryTransform);
                    DestroyTransform(categoryTransform);
                }
            }

            _categoryRoots.Clear();
        }

        private void BuildViewQuadEntries()
        {
            LayoutObject layout = Editor.Instance?.Project?.Layout;
            if (layout == null)
            {
                return;
            }

            List<View> views = layout.GetViews();
            if (views == null || views.Count == 0)
            {
                return;
            }

            Transform parentTransform = GetOrCreateCategoryTransform(kViewQuadCategoryName);

            for (int i = 0; i < views.Count; i++)
            {
                View view = views[i];
                if (view == null)
                {
                    continue;
                }

                GameObject entryObject = new GameObject(GetViewQuadDisplayName(view))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                Transform entryTransform = entryObject.transform;
                entryTransform.SetParent(parentTransform, false);

                UnityAction onChangedHandler = () => UpdateViewQuadEntryName(view);
                view.OnChanged.AddListener(onChangedHandler);

                _viewQuadEntries[view] = new ViewQuadEntry(entryTransform, onChangedHandler);
                _transformToViewQuad[entryTransform] = view;
            }
        }

        private void ClearViewQuadEntries()
        {
            if (_viewQuadEntries.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<View, ViewQuadEntry> pair in _viewQuadEntries)
            {
                View view = pair.Key;
                ViewQuadEntry entry = pair.Value;

                if (view != null && entry.OnChangedHandler != null)
                {
                    view.OnChanged.RemoveListener(entry.OnChangedHandler);
                }

                if (entry.Transform != null)
                {
                    _transformToViewQuad.Remove(entry.Transform);
                    DestroyTransform(entry.Transform);
                }
            }

            _viewQuadEntries.Clear();
            _transformToViewQuad.Clear();
        }

        private void AddComponentEntry(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (_componentEntries.ContainsKey(component))
            {
                UpdateComponentEntryName(component);
                return;
            }

            string categoryName = GetCategoryNameForComponent(component);
            Transform parentTransform = GetOrCreateCategoryTransform(categoryName);

            GameObject entryObject = new GameObject(GetComponentDisplayName(component))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Transform entryTransform = entryObject.transform;
            entryTransform.SetParent(parentTransform, false);

            Layout.Component.OnValueSetDelegate handler = _ => UpdateComponentEntryName(component);
            component.OnValueSet += handler;

            _componentEntries[component] = new ComponentEntry(entryTransform, handler);
            _transformToComponent[entryTransform] = component;
        }

        private void RemoveComponentEntry(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (!_componentEntries.TryGetValue(component, out ComponentEntry entry))
            {
                return;
            }

            component.OnValueSet -= entry.ValueChangedHandler;

            if (entry.Transform != null)
            {
                _transformToComponent.Remove(entry.Transform);
                DestroyTransform(entry.Transform);
            }

            _componentEntries.Remove(component);
        }

        private void ClearComponentEntries()
        {
            if (_componentEntries.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Component, ComponentEntry> pair in _componentEntries)
            {
                Component component = pair.Key;
                ComponentEntry entry = pair.Value;

                if (component != null)
                {
                    component.OnValueSet -= entry.ValueChangedHandler;
                }

                if (entry.Transform != null)
                {
                    _transformToComponent.Remove(entry.Transform);
                    DestroyTransform(entry.Transform);
                }
            }

            _componentEntries.Clear();
            _transformToComponent.Clear();
        }

        private void OnLayoutComponentAdded(Component component, View view)
        {
            if (!IsTargetView(view))
            {
                return;
            }

            if (_currentView == null && view != null)
            {
                _currentView = view;
                if (string.IsNullOrEmpty(_currentViewName))
                {
                    _currentViewName = view.Name;
                }
            }

            EnsureCategoryRoots();
            AddComponentEntry(component);
            _runtimeHierarchy?.Refresh();
        }

        private void OnLayoutComponentRemoved(Component component, View view)
        {
            if (!IsTargetView(view))
            {
                return;
            }

            if (_currentView == null && view != null)
            {
                _currentView = view;
                if (string.IsNullOrEmpty(_currentViewName))
                {
                    _currentViewName = view.Name;
                }
            }

            RemoveComponentEntry(component);
            _runtimeHierarchy?.Refresh();
        }

        private bool IsTargetView(View view)
        {
            if (view == null)
            {
                return false;
            }

            if (_currentView != null && ReferenceEquals(_currentView, view))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(_currentViewName) && string.Equals(view.Name, _currentViewName, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private void UpdateComponentEntryName(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (_componentEntries.TryGetValue(component, out ComponentEntry entry) && entry.Transform != null)
            {
                entry.Transform.gameObject.name = GetComponentDisplayName(component);
            }
        }

        private void UpdateViewQuadEntryName(View view)
        {
            if (view == null)
            {
                return;
            }

            if (_viewQuadEntries.TryGetValue(view, out ViewQuadEntry entry) && entry.Transform != null)
            {
                entry.Transform.gameObject.name = GetViewQuadDisplayName(view);
            }
        }

        private static string GetComponentDisplayName(Component component)
        {
            if (component == null)
            {
                return "<missing>";
            }

            string baseName = component.Name;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = component.GetType().Name;
            }

            string typeName = component.GetType().Name;

            if (!string.Equals(baseName, typeName, StringComparison.Ordinal))
            {
                return $"{baseName} ({typeName})";
            }

            return baseName;
        }

        private static string GetViewQuadDisplayName(View view)
        {
            if (view == null)
            {
                return "<missing view quad>";
            }

            string viewName = view.Name;
            string quadName = view.Data?.ViewQuad?.Name;

            bool hasQuadName = !string.IsNullOrWhiteSpace(quadName);
            bool hasViewName = !string.IsNullOrWhiteSpace(viewName);

            if (hasQuadName && hasViewName && !string.Equals(quadName, viewName, StringComparison.Ordinal))
            {
                return $"{quadName} ({viewName} View Quad)";
            }

            if (hasQuadName)
            {
                return $"{quadName} (View Quad)";
            }

            if (hasViewName)
            {
                return $"{viewName} (View Quad)";
            }

            return "View Quad";
        }

        private EditorComponent FindEditorComponent(Component component)
        {
            if (component == null)
            {
                return null;
            }

            if (_currentEditorView != null)
            {
                EditorComponent editorComponent = _currentEditorView.GetEditorComponent(component);
                if (editorComponent != null)
                {
                    return editorComponent;
                }
            }

            if (Editor.Instance?.UIController?.DynamicPanelsCanvas == null)
            {
                return null;
            }

            EditorView[] editorViews = Editor.Instance.UIController.DynamicPanelsCanvas.GetComponentsInChildren<EditorView>(true);
            for (int i = 0; i < editorViews.Length; i++)
            {
                EditorView editorView = editorViews[i];
                if (editorView == null || ReferenceEquals(editorView, _currentEditorView))
                {
                    continue;
                }

                EditorComponent editorComponent = editorView.GetEditorComponent(component);
                if (editorComponent != null)
                {
                    return editorComponent;
                }
            }

            return null;
        }

        private void OnViewQuadHandleSelected(BaseViewQuadOverlay overlay, int handleIndex)
        {
            if (_ignoreViewQuadSelectionEvents)
            {
                return;
            }

            if (overlay == null || overlay.View == null)
            {
                return;
            }

            _selectedViewQuad = overlay.View;
            UpdateHierarchySelectionFromEditor();
        }

        private void OnViewQuadHandleSelectionCleared(BaseViewQuadOverlay overlay)
        {
            if (_ignoreViewQuadSelectionEvents)
            {
                return;
            }

            if (overlay == null || overlay.View == null)
            {
                return;
            }

            if (ReferenceEquals(_selectedViewQuad, overlay.View))
            {
                _selectedViewQuad = null;
                UpdateHierarchySelectionFromEditor();
            }
        }

        private void FocusViewQuad(View view)
        {
            BaseViewQuadOverlay overlay = GetOverlayForView(view);
            if (overlay == null)
            {
                return;
            }

            overlay.SetActive(true);

            if (overlay.PointCount <= 0)
            {
                return;
            }

            int handleIndex = overlay.HasSelectedHandle ? overlay.SelectedHandleIndex : 0;
            handleIndex = Mathf.Clamp(handleIndex, 0, overlay.PointCount - 1);

            if (handleIndex < 0)
            {
                return;
            }

            _ignoreViewQuadSelectionEvents = true;
            try
            {
                overlay.SelectHandle(handleIndex);
            }
            finally
            {
                _ignoreViewQuadSelectionEvents = false;
            }
        }

        private BaseViewQuadOverlay GetOverlayForView(View view)
        {
            if (view == null)
            {
                return null;
            }

            if (_viewToOverlay.TryGetValue(view, out BaseViewQuadOverlay overlay) && overlay != null)
            {
                return overlay;
            }

            BaseViewQuadOverlay[] overlays = Resources.FindObjectsOfTypeAll<BaseViewQuadOverlay>();
            if (overlays == null)
            {
                return null;
            }

            for (int i = 0; i < overlays.Length; i++)
            {
                BaseViewQuadOverlay candidate = overlays[i];
                if (candidate == null || candidate.View == null)
                {
                    continue;
                }

                _viewToOverlay[candidate.View] = candidate;

                if (candidate.View == view)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string GetCategoryNameForComponent(Component component)
        {
            return component switch
            {
                ComponentBackground => "Backgrounds",
                ComponentReel => "Reels",
                ComponentAlpha => "Alphas",
                ComponentLamp => "Lamps",
                Component7Segment => "7 Segments",
                Component14Segment => "7 Segments",
                Component14SemicolonSegment => "7 Segments",
                Component16Segment => "7 Segments",
                Component16SemicolonSegment => "7 Segments",
                _ => kFallbackCategoryName
            };
        }

        private static void DestroyTransform(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(transform.gameObject);
            }
            else
            {
                DestroyImmediate(transform.gameObject);
            }
        }

        private readonly struct ComponentEntry
        {
            public ComponentEntry(Transform transform, Component.OnValueSetDelegate valueChangedHandler)
            {
                Transform = transform;
                ValueChangedHandler = valueChangedHandler;
            }

            public Transform Transform { get; }
            public Component.OnValueSetDelegate ValueChangedHandler { get; }
        }

        private readonly struct ViewQuadEntry
        {
            public ViewQuadEntry(Transform transform, UnityAction onChangedHandler)
            {
                Transform = transform;
                OnChangedHandler = onChangedHandler;
            }

            public Transform Transform { get; }
            public UnityAction OnChangedHandler { get; }
        }
    }
}
