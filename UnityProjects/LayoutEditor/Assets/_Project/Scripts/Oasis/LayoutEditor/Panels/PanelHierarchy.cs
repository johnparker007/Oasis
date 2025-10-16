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
        private const string kLegacyComponentNamePrefix = "(Legacy) ";

        [SerializeField]
        private RuntimeHierarchy _runtimeHierarchy = null;

        private RuntimeHierarchyStandaloneTransformCollection _transformCollection;
        private readonly Dictionary<string, Transform> _categoryRoots = new(StringComparer.Ordinal);
        private readonly Dictionary<Component, ComponentEntry> _componentEntries = new();
        private readonly Dictionary<Transform, Component> _transformToComponent = new();
        private Transform _viewQuadsRoot;
        private readonly Dictionary<ViewQuadKey, ViewQuadEntry> _viewQuadEntries = new();
        private readonly Dictionary<Transform, ViewQuadKey> _transformToViewQuad = new();

        private LayoutObject _observedLayout;
        private View _currentView;
        private string _currentViewName;
        private EditorView _currentEditorView;
        private bool _eventsSubscribed;
        private bool _runtimeHierarchyEventsSubscribed;
        private bool _suppressSelectionChangeHandling;
        private bool _suppressHierarchySelectionChange;

        private void Awake()
        {
            EnsureRuntimeHierarchy();
        }

        private void OnEnable()
        {
            if (Editor.Instance != null)
            {
                Editor.Instance.HierarchyPanel = this;
            }

            EnsureRuntimeHierarchy();
            EnsureCategoryRoots();
            EnsureViewQuadRoot();

            if (string.IsNullOrEmpty(_currentViewName))
            {
                _currentViewName = ViewController.kBaseViewName;
            }

            SubscribeToEditorEvents();
            SubscribeToLayout(Editor.Instance != null ? Editor.Instance.Project?.Layout : null);

            RefreshActiveView();
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            if (Editor.Instance != null && ReferenceEquals(Editor.Instance.HierarchyPanel, this))
            {
                Editor.Instance.HierarchyPanel = null;
            }

            UnsubscribeFromLayout();
            UnsubscribeFromEditorEvents();
            UnsubscribeFromRuntimeHierarchyEvents();

            ClearComponentEntries();
            ClearViewQuadEntries();
            DestroyCategoryRoots();
            DestroyViewQuadRoot();

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
                _transformCollection = new RuntimeHierarchyStandaloneTransformCollection(
                    _runtimeHierarchy,
                    expandEntries: false);
            }

            SubscribeToRuntimeHierarchyEvents();
        }

        private void SubscribeToRuntimeHierarchyEvents()
        {
            if (_runtimeHierarchyEventsSubscribed || _runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchyEventsSubscribed = true;
        }

        private void UnsubscribeFromRuntimeHierarchyEvents()
        {
            if (!_runtimeHierarchyEventsSubscribed || _runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchyEventsSubscribed = false;
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
                Editor.Instance.SelectionController.OnSelectionChange.AddListener(OnSelectionChanged);
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
                Editor.Instance.SelectionController.OnSelectionChange.RemoveListener(OnSelectionChanged);
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
            layout.OnAddView.AddListener(OnLayoutViewAdded);
            layout.OnRemoveView.AddListener(OnLayoutViewRemoved);

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
            _observedLayout.OnAddView.RemoveListener(OnLayoutViewAdded);
            _observedLayout.OnRemoveView.RemoveListener(OnLayoutViewRemoved);
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

        public View GetActiveView()
        {
            return ResolveCurrentView();
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
            EnsureViewQuadRoot();
            RebuildViewQuadEntries();

            View activeView = ResolveCurrentView();

            if (activeView == null)
            {
                _runtimeHierarchy.Refresh();
                OnSelectionChanged();
                return;
            }

            foreach (Component component in activeView.Data.Components)
            {
                AddComponentEntry(component);
            }

            _runtimeHierarchy.Refresh();
            OnSelectionChanged();
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

        private void EnsureViewQuadRoot()
        {
            GetOrCreateViewQuadRoot();
        }

        private Transform GetOrCreateViewQuadRoot()
        {
            if (_viewQuadsRoot != null)
            {
                return _viewQuadsRoot;
            }

            if (_transformCollection == null)
            {
                return null;
            }

            _viewQuadsRoot = CreateCategoryTransform(kViewQuadCategoryName);
            _transformCollection.Add(_viewQuadsRoot);

            return _viewQuadsRoot;
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

        private void DestroyViewQuadRoot()
        {
            if (_viewQuadsRoot == null)
            {
                return;
            }

            _transformCollection?.Remove(_viewQuadsRoot);
            DestroyTransform(_viewQuadsRoot);
            _viewQuadsRoot = null;
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

        private void ClearViewQuadEntries()
        {
            if (_viewQuadEntries.Count == 0)
            {
                return;
            }

            foreach (ViewQuadKey key in _viewQuadEntries.Keys.ToList())
            {
                RemoveViewQuadEntry(key);
            }

            _viewQuadEntries.Clear();
            _transformToViewQuad.Clear();
        }

        private void RebuildViewQuadEntries()
        {
            Transform parent = GetOrCreateViewQuadRoot();
            if (parent == null)
            {
                return;
            }

            LayoutObject layout = Editor.Instance != null ? Editor.Instance.Project?.Layout : null;
            if (layout == null)
            {
                return;
            }

            List<View> views = layout.GetViews();
            if (views == null)
            {
                return;
            }

            foreach (View view in views)
            {
                if (view?.ViewQuads == null || view.ViewQuads.Count == 0)
                {
                    RemoveViewQuadEntries(view);
                    continue;
                }

                foreach (ViewQuad viewQuad in view.ViewQuads)
                {
                    AddViewQuadEntry(view, viewQuad, parent);
                }
            }
        }

        private void AddViewQuadEntry(View view, ViewQuad viewQuad, Transform parent)
        {
            if (view == null || viewQuad == null || parent == null)
            {
                return;
            }

            if (view.ViewQuads == null || !view.ViewQuads.Contains(viewQuad))
            {
                view.TrySetActiveViewQuad(viewQuad);
                if (view.ViewQuads == null || !view.ViewQuads.Contains(viewQuad))
                {
                    return;
                }
            }

            ViewQuadKey key = new ViewQuadKey(view, viewQuad);

            if (_viewQuadEntries.ContainsKey(key))
            {
                UpdateViewQuadEntryName(key);
                return;
            }

            GameObject entryObject = new GameObject(GetViewQuadDisplayName(key))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Transform entryTransform = entryObject.transform;
            entryTransform.SetParent(parent, false);

            UnityAction handler = () => UpdateViewQuadEntryName(key);
            view.OnChanged.AddListener(handler);

            _viewQuadEntries[key] = new ViewQuadEntry(entryTransform, handler);
            _transformToViewQuad[entryTransform] = key;
        }

        private void RemoveViewQuadEntry(ViewQuadKey key)
        {
            if (!_viewQuadEntries.TryGetValue(key, out ViewQuadEntry entry))
            {
                return;
            }

            if (entry.ViewChangedHandler != null)
            {
                key.View?.OnChanged.RemoveListener(entry.ViewChangedHandler);
            }

            if (entry.Transform != null)
            {
                _transformToViewQuad.Remove(entry.Transform);
                DestroyTransform(entry.Transform);
            }

            _viewQuadEntries.Remove(key);
        }

        private void RemoveViewQuadEntries(View view)
        {
            if (view == null)
            {
                return;
            }

            List<ViewQuadKey> keysToRemove = _viewQuadEntries.Keys
                .Where(key => ReferenceEquals(key.View, view))
                .ToList();

            foreach (ViewQuadKey key in keysToRemove)
            {
                RemoveViewQuadEntry(key);
            }
        }

        private void UpdateViewQuadEntryName(ViewQuadKey key)
        {
            if (!key.IsValid)
            {
                RemoveViewQuadEntry(key);
                return;
            }

            if (key.View.ViewQuads == null || !key.View.ViewQuads.Contains(key.ViewQuad))
            {
                RemoveViewQuadEntry(key);
                return;
            }

            if (_viewQuadEntries.TryGetValue(key, out ViewQuadEntry entry) && entry.Transform != null)
            {
                GameObject entryObject = entry.Transform.gameObject;
                if (entryObject == null)
                {
                    return;
                }

                string newName = GetViewQuadDisplayName(key);
                if (!string.Equals(entryObject.name, newName, StringComparison.Ordinal))
                {
                    entryObject.name = newName;
                    _runtimeHierarchy?.Refresh();
                }
            }
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
            OnSelectionChanged();
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
            OnSelectionChanged();
        }

        private void OnLayoutViewAdded(View view)
        {
            Transform parent = GetOrCreateViewQuadRoot();
            if (parent == null)
            {
                return;
            }

            if (view?.ViewQuads != null)
            {
                foreach (ViewQuad viewQuad in view.ViewQuads)
                {
                    AddViewQuadEntry(view, viewQuad, parent);
                }
            }
            _runtimeHierarchy?.Refresh();
        }

        private void OnLayoutViewRemoved(View view)
        {
            RemoveViewQuadEntries(view);
            _runtimeHierarchy?.Refresh();

            if (view != null && ReferenceEquals(_currentView, view))
            {
                SelectFallbackView();
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

        private void OnSelectionChanged()
        {
            if (_suppressSelectionChangeHandling)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance != null
                ? Editor.Instance.SelectionController
                : null;

            if (selectionController == null || _runtimeHierarchy == null)
            {
                return;
            }

            IReadOnlyList<EditorComponent> selectedComponents = selectionController.SelectedEditorComponents;

            if (selectedComponents == null || selectedComponents.Count == 0)
            {
                if (!_suppressHierarchySelectionChange)
                {
                    _suppressHierarchySelectionChange = true;
                    _runtimeHierarchy.Deselect();
                    _suppressHierarchySelectionChange = false;
                }
                return;
            }

            for (int i = 0; i < selectedComponents.Count; i++)
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
                    HighlightHierarchyEntry(entry.Transform);
                    return;
                }
            }

            if (!_suppressHierarchySelectionChange)
            {
                _suppressHierarchySelectionChange = true;
                _runtimeHierarchy.Deselect();
                _suppressHierarchySelectionChange = false;
            }
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

        private void HighlightHierarchyEntry(Transform entryTransform)
        {
            if (_runtimeHierarchy == null || entryTransform == null)
            {
                return;
            }

            _suppressHierarchySelectionChange = true;
            _runtimeHierarchy.Select(
                entryTransform,
                RuntimeHierarchy.SelectOptions.ForceRevealSelection | RuntimeHierarchy.SelectOptions.FocusOnSelection);
            _suppressHierarchySelectionChange = false;
        }

        public void EnsureViewQuadEntry(View view, ViewQuad viewQuad)
        {
            ViewQuadKey key = new ViewQuadKey(view, viewQuad);
            if (!key.IsValid)
            {
                return;
            }

            EnsureViewQuadEntryTransform(key);
        }

        public void HighlightViewQuad(View view, ViewQuad viewQuad)
        {
            ViewQuadKey key = new ViewQuadKey(view, viewQuad);
            if (!key.IsValid)
            {
                return;
            }

            Transform entryTransform = EnsureViewQuadEntryTransform(key);
            if (entryTransform == null)
            {
                return;
            }

            key.View?.TrySetActiveViewQuad(key.ViewQuad);
            HighlightHierarchyEntry(entryTransform);

            InspectorController inspectorController = Editor.Instance != null
                ? Editor.Instance.InspectorController
                : null;

            inspectorController?.ShowViewQuad(key.View, key.ViewQuad);
        }

        private Transform EnsureViewQuadEntryTransform(ViewQuadKey key)
        {
            if (!key.IsValid)
            {
                return null;
            }

            if (_viewQuadEntries.TryGetValue(key, out ViewQuadEntry entry))
            {
                if (entry.Transform != null)
                {
                    return entry.Transform;
                }

                RemoveViewQuadEntry(key);
            }

            Transform parent = GetOrCreateViewQuadRoot();
            if (parent == null)
            {
                return null;
            }

            bool alreadyHadEntry = _viewQuadEntries.ContainsKey(key);
            AddViewQuadEntry(key.View, key.ViewQuad, parent);

            if (_viewQuadEntries.TryGetValue(key, out entry) && entry.Transform != null)
            {
                if (!alreadyHadEntry)
                {
                    _runtimeHierarchy?.Refresh();
                }

                return entry.Transform;
            }

            return null;
        }

        private EditorComponent FindEditorComponent(Component component)
        {
            if (component == null)
            {
                return null;
            }

            EditorView editorView = _currentEditorView;
            if (editorView == null)
            {
                View currentView = ResolveCurrentView();
                if (currentView != null)
                {
                    editorView = currentView.EditorView;
                }
            }

            if (editorView == null)
            {
                return null;
            }

            return editorView.GetEditorComponent(component);
        }

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            if (_suppressHierarchySelectionChange)
            {
                return;
            }

            SelectionController selectionController = Editor.Instance != null
                ? Editor.Instance.SelectionController
                : null;

            if (selectionController == null)
            {
                return;
            }

            Component componentToSelect = null;
            ViewQuadKey viewQuadToSelect = default;
            bool hasViewQuadSelection = false;

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
                        componentToSelect = component;
                        break;
                    }

                    if (_transformToViewQuad.TryGetValue(transform, out ViewQuadKey key) && key.IsValid)
                    {
                        viewQuadToSelect = key;
                        hasViewQuadSelection = true;
                        break;
                    }
                }
            }

            _suppressSelectionChangeHandling = true;

            if (componentToSelect != null)
            {
                EditorComponent editorComponent = FindEditorComponent(componentToSelect);
                if (editorComponent != null)
                {
                    IReadOnlyList<EditorComponent> currentSelection = selectionController.SelectedEditorComponents;
                    bool alreadySelected = currentSelection != null && currentSelection.Count == 1 && currentSelection[0] == editorComponent;

                    if (!alreadySelected)
                    {
                        selectionController.DeselectAllObjects();
                        selectionController.SelectObject(editorComponent);
                    }
                }
            }
            else if (hasViewQuadSelection)
            {
                selectionController.DeselectAllObjects();
                viewQuadToSelect.View?.TrySetActiveViewQuad(viewQuadToSelect.ViewQuad);
                Editor.Instance?.InspectorController?.ShowViewQuad(viewQuadToSelect.View, viewQuadToSelect.ViewQuad);
            }
            else if (selection == null || selection.Count == 0)
            {
                selectionController.DeselectAllObjects();
            }
            else
            {
                selectionController.DeselectAllObjects();
            }

            _suppressSelectionChangeHandling = false;
        }

        private static string GetComponentDisplayName(Component component)
        {
            if (component == null)
            {
                return "<missing>";
            }

            switch (component)
            {
                case ComponentBackground:
                    return FormatLegacyComponentDisplayName("Background", null);
                case ComponentReel reel:
                    return FormatLegacyComponentDisplayName("Reel", reel.Number);
                case Component7Segment segment7:
                    return FormatLegacyComponentDisplayName("7 Segment", segment7.Number);
                case ComponentLamp lamp:
                    return FormatLegacyComponentDisplayName("Lamp", lamp.Number);
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

        private static string FormatLegacyComponentDisplayName(string componentTypeDisplayName, int? number)
        {
            if (number.HasValue)
            {
                return $"{kLegacyComponentNamePrefix}{componentTypeDisplayName} ({number.Value})";
            }

            return $"{kLegacyComponentNamePrefix}{componentTypeDisplayName}";
        }

        private static string GetViewQuadDisplayName(ViewQuadKey key)
        {
            View view = key.View;
            ViewQuad viewQuad = key.ViewQuad;

            if (view == null || viewQuad == null)
            {
                return "<missing view quad>";
            }

            string viewName = view.Name;
            if (string.IsNullOrWhiteSpace(viewName))
            {
                viewName = "<unnamed view>";
            }

            string quadName = viewQuad.Name;
            if (!string.IsNullOrWhiteSpace(quadName))
            {
                return $"{viewName} View Quad ({quadName})";
            }

            return $"{viewName} View Quad";
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

        private readonly struct ViewQuadKey : IEquatable<ViewQuadKey>
        {
            public ViewQuadKey(View view, ViewQuad viewQuad)
            {
                View = view;
                ViewQuad = viewQuad;
            }

            public View View { get; }

            public ViewQuad ViewQuad { get; }

            public bool IsValid => View != null && ViewQuad != null;

            public bool Equals(ViewQuadKey other)
            {
                return ReferenceEquals(View, other.View) && ReferenceEquals(ViewQuad, other.ViewQuad);
            }

            public override bool Equals(object obj)
            {
                return obj is ViewQuadKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int viewHash = View != null ? View.GetHashCode() : 0;
                    int quadHash = ViewQuad != null ? ViewQuad.GetHashCode() : 0;
                    return (viewHash * 397) ^ quadHash;
                }
            }
        }

        private readonly struct ViewQuadEntry
        {
            public ViewQuadEntry(Transform transform, UnityAction viewChangedHandler)
            {
                Transform = transform;
                ViewChangedHandler = viewChangedHandler;
            }

            public Transform Transform { get; }
            public UnityAction ViewChangedHandler { get; }
        }
    }
}
