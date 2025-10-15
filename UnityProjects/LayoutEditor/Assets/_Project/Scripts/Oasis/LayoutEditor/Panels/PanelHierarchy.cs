using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Oasis.Layout;
using Oasis.LayoutEditor;
using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.Events;
using Component = Oasis.Layout.Component;
using View = Oasis.Layout.View;

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
        private readonly Dictionary<Transform, Component> _componentsByTransform = new();
        private readonly Dictionary<View, ViewQuadEntry> _viewQuadEntries = new();
        private readonly Dictionary<Transform, View> _viewQuadTransforms = new();
        private readonly List<Transform> _runtimeSelectionBuffer = new();
        private readonly List<EditorComponent> _hierarchySelectionBuffer = new();

        private LayoutObject _observedLayout;
        private View _currentView;
        private string _currentViewName;
        private EditorView _currentEditorView;
        private bool _eventsSubscribed;
        private bool _selectionEventsSubscribed;
        private bool _isApplyingEditorSelection;
        private bool _isApplyingHierarchySelection;

        private void Awake()
        {
            EnsureRuntimeHierarchy();
        }

        private void OnEnable()
        {
            EnsureRuntimeHierarchy();
            EnsureCategoryRoots();
            SubscribeToHierarchySelection();

            if (string.IsNullOrEmpty(_currentViewName))
            {
                _currentViewName = ViewController.kBaseViewName;
            }

            SubscribeToEditorEvents();
            SubscribeToSelectionEvents();
            SubscribeToLayout(Editor.Instance != null ? Editor.Instance.Project?.Layout : null);

            RefreshActiveView();
        }

        private void OnDisable()
        {
            UnsubscribeFromLayout();
            UnsubscribeFromEditorEvents();
            UnsubscribeFromSelectionEvents();
            UnsubscribeFromHierarchySelection();

            ClearComponentEntries();
            ClearViewQuadEntries();
            DestroyCategoryRoots();

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

            SubscribeToHierarchySelection();
        }

        private void SubscribeToHierarchySelection()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
        }

        private void UnsubscribeFromHierarchySelection()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
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

            _eventsSubscribed = false;
        }

        private void SubscribeToSelectionEvents()
        {
            if (_selectionEventsSubscribed)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController == null)
            {
                return;
            }

            selectionController.OnSelectionChange.AddListener(OnEditorSelectionChanged);
            _selectionEventsSubscribed = true;
        }

        private void UnsubscribeFromSelectionEvents()
        {
            if (!_selectionEventsSubscribed)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController != null)
            {
                selectionController.OnSelectionChange.RemoveListener(OnEditorSelectionChanged);
            }

            _selectionEventsSubscribed = false;
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
            EnsureViewQuadCategoryRoot();
            AddViewQuadEntries();

            View activeView = ResolveCurrentView();

            if (activeView == null)
            {
                _runtimeHierarchy.Refresh();
                return;
            }

            foreach (Component component in activeView.Data.Components)
            {
                AddComponentEntry(component);
            }

            _runtimeHierarchy.Refresh();
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

        private EditorView GetActiveEditorView()
        {
            if (_currentEditorView != null && _currentEditorView.isActiveAndEnabled)
            {
                return _currentEditorView;
            }

            View view = ResolveCurrentView();
            EditorView editorView = view?.EditorView;

            if (editorView != null && editorView.isActiveAndEnabled)
            {
                return editorView;
            }

            return _currentEditorView ?? editorView;
        }

        private EditorComponent GetEditorComponentForLayoutComponent(Component component)
        {
            if (component == null)
            {
                return null;
            }

            EditorView editorView = GetActiveEditorView();
            if (editorView == null)
            {
                return null;
            }

            return editorView.GetEditorComponent(component);
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

        private void EnsureViewQuadCategoryRoot()
        {
            GetOrCreateCategoryTransform(kViewQuadCategoryName);
        }

        private void AddViewQuadEntries()
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
            if (parentTransform == null)
            {
                return;
            }

            for (int i = 0; i < views.Count; ++i)
            {
                AddViewQuadEntry(views[i], parentTransform);
            }
        }

        private void AddViewQuadEntry(View view, Transform parentTransform)
        {
            if (view == null || parentTransform == null)
            {
                return;
            }

            if (_viewQuadEntries.ContainsKey(view))
            {
                UpdateViewQuadEntryName(view);
                return;
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
            _viewQuadTransforms[entryTransform] = view;
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

        private void SelectViewQuadFromHierarchy(View view)
        {
            if (view == null)
            {
                return;
            }

            EditorView editorView = view.EditorView;
            if (editorView == null)
            {
                return;
            }

            BaseViewQuadOverlay overlay = editorView.GetComponentInChildren<BaseViewQuadOverlay>(true);
            if (overlay == null)
            {
                return;
            }

            if (!overlay.gameObject.activeSelf)
            {
                overlay.gameObject.SetActive(true);
            }

            if (overlay.HasSelectedHandle)
            {
                overlay.SelectHandle(overlay.SelectedHandleIndex);
            }
            else
            {
                overlay.SelectHandle(0);
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
                    _viewQuadTransforms.Remove(entry.Transform);
                    DestroyTransform(entry.Transform);
                }
            }

            _viewQuadEntries.Clear();
            _viewQuadTransforms.Clear();
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
            _componentsByTransform[entryTransform] = component;
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
                _componentsByTransform.Remove(entry.Transform);
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
                    _componentsByTransform.Remove(entry.Transform);
                    DestroyTransform(entry.Transform);
                }
            }

            _componentEntries.Clear();
            _componentsByTransform.Clear();
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

        private void OnEditorSelectionChanged()
        {
            if (_runtimeHierarchy == null || _isApplyingHierarchySelection)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController == null)
            {
                return;
            }

            try
            {
                _isApplyingEditorSelection = true;

                _runtimeSelectionBuffer.Clear();

                List<EditorComponent> selectedComponents = selectionController.SelectedEditorComponents;
                for (int i = 0; i < selectedComponents.Count; ++i)
                {
                    EditorComponent editorComponent = selectedComponents[i];
                    if (editorComponent == null)
                    {
                        continue;
                    }

                    Component layoutComponent = editorComponent.Component;
                    if (layoutComponent == null)
                    {
                        continue;
                    }

                    if (_componentEntries.TryGetValue(layoutComponent, out ComponentEntry entry) && entry.Transform != null)
                    {
                        if (!_runtimeSelectionBuffer.Contains(entry.Transform))
                        {
                            _runtimeSelectionBuffer.Add(entry.Transform);
                        }
                    }
                }

                if (_runtimeSelectionBuffer.Count > 0)
                {
                    _runtimeHierarchy.Select(
                        _runtimeSelectionBuffer,
                        RuntimeHierarchy.SelectOptions.ForceRevealSelection | RuntimeHierarchy.SelectOptions.FocusOnSelection);
                }
                else
                {
                    _runtimeHierarchy.Deselect();
                }
            }
            finally
            {
                _runtimeSelectionBuffer.Clear();
                _isApplyingEditorSelection = false;
            }
        }

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            if (_isApplyingEditorSelection)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance?.SelectionController;
            if (selectionController == null)
            {
                return;
            }

            try
            {
                _isApplyingHierarchySelection = true;

                _hierarchySelectionBuffer.Clear();
                View selectedViewQuad = null;

                if (selection != null)
                {
                    for (int i = 0; i < selection.Count; ++i)
                    {
                        Transform selectedTransform = selection[i];
                        if (selectedTransform == null)
                        {
                            continue;
                        }

                        if (_componentsByTransform.TryGetValue(selectedTransform, out Component component) && component != null)
                        {
                            EditorComponent editorComponent = GetEditorComponentForLayoutComponent(component);
                            if (editorComponent != null && !_hierarchySelectionBuffer.Contains(editorComponent))
                            {
                                _hierarchySelectionBuffer.Add(editorComponent);
                            }
                        }
                        else if (_viewQuadTransforms.TryGetValue(selectedTransform, out View view) && view != null)
                        {
                            selectedViewQuad = view;
                        }
                    }
                }

                if (_hierarchySelectionBuffer.Count > 0)
                {
                    selectionController.DeselectAllObjects();
                    for (int i = 0; i < _hierarchySelectionBuffer.Count; ++i)
                    {
                        selectionController.SelectObject(_hierarchySelectionBuffer[i]);
                    }
                }
                else if (selectedViewQuad != null)
                {
                    selectionController.DeselectAllObjects();
                    SelectViewQuadFromHierarchy(selectedViewQuad);
                }
                else if (selection == null || selection.Count == 0)
                {
                    selectionController.DeselectAllObjects();
                }
            }
            finally
            {
                _hierarchySelectionBuffer.Clear();
                _isApplyingHierarchySelection = false;
            }
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

        private static string GetViewQuadDisplayName(View view)
        {
            if (view == null)
            {
                return "<missing view quad>";
            }

            string viewName = !string.IsNullOrWhiteSpace(view.Name) ? view.Name : "<unnamed view>";
            string quadName = view.Data?.ViewQuad?.Name;

            if (!string.IsNullOrWhiteSpace(quadName))
            {
                return $"{viewName} View Quad ({quadName})";
            }

            return $"{viewName} View Quad";
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
