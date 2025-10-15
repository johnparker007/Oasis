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
            "Lamps",
            kViewQuadsCategoryName
        };

        private const string kViewQuadsCategoryName = "View Quads";
        private const string kFallbackCategoryName = "Components";

        [SerializeField]
        private RuntimeHierarchy _runtimeHierarchy = null;

        private RuntimeHierarchyStandaloneTransformCollection _transformCollection;
        private readonly Dictionary<string, Transform> _categoryRoots = new(StringComparer.Ordinal);
        private readonly Dictionary<Component, ComponentEntry> _componentEntries = new();
        private readonly Dictionary<Transform, Component> _componentsByTransform = new();
        private readonly Dictionary<View, ViewQuadEntry> _viewQuadEntries = new();
        private readonly Dictionary<Transform, View> _viewsByTransform = new();

        private LayoutObject _observedLayout;
        private View _currentView;
        private string _currentViewName;
        private EditorView _currentEditorView;
        private bool _eventsSubscribed;
        private bool _hierarchySelectionSubscribed;
        private bool _selectionEventsSubscribed;
        private bool _inspectorEventsSubscribed;
        private bool _suppressHierarchySelectionChange;
        private bool _suppressSelectionControllerEvents;

        private View _currentViewQuadSelection;

        private void Awake()
        {
            EnsureRuntimeHierarchy();
        }

        private void OnEnable()
        {
            EnsureRuntimeHierarchy();
            EnsureCategoryRoots();

            if (string.IsNullOrEmpty(_currentViewName))
            {
                _currentViewName = ViewController.kBaseViewName;
            }

            SubscribeToEditorEvents();
            SubscribeToLayout(Editor.Instance != null ? Editor.Instance.Project?.Layout : null);
            SubscribeToRuntimeHierarchySelection();
            SubscribeToSelectionController();
            SubscribeToInspectorEvents();

            RefreshActiveView();
        }

        private void OnDisable()
        {
            UnsubscribeFromLayout();
            UnsubscribeFromEditorEvents();
            UnsubscribeFromRuntimeHierarchySelection();
            UnsubscribeFromSelectionController();
            UnsubscribeFromInspectorEvents();

            ClearComponentEntries();
            ClearViewQuadEntries();
            DestroyCategoryRoots();

            _currentView = null;
            _currentEditorView = null;
            _currentViewName = null;
            _currentViewQuadSelection = null;
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

            if (!ReferenceEquals(_currentViewQuadSelection, view))
            {
                _currentViewQuadSelection = null;
            }

            RebuildHierarchyEntries();
        }

        private void RebuildHierarchyEntries()
        {
            EnsureRuntimeHierarchy();

            if (_runtimeHierarchy == null)
            {
                ClearComponentEntries();
                return;
            }

            _runtimeHierarchy.Deselect();

            ClearComponentEntries();
            ClearViewQuadEntries();
            EnsureCategoryRoots();

            View activeView = ResolveCurrentView();

            if (activeView == null)
            {
                _runtimeHierarchy.Refresh();
                DeselectHierarchy();
                return;
            }

            AddViewQuadEntry(activeView);

            foreach (Component component in activeView.Data.Components)
            {
                AddComponentEntry(component);
            }

            _runtimeHierarchy.Refresh();
            RefreshHierarchySelection();
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
                DestroyTransform(entry.Transform);
            }

            _componentEntries.Remove(component);
            if (entry.Transform != null)
            {
                _componentsByTransform.Remove(entry.Transform);
            }
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

        private void AddViewQuadEntry(View view)
        {
            if (view == null || view.Data?.ViewQuad == null)
            {
                return;
            }

            if (_viewQuadEntries.ContainsKey(view))
            {
                UpdateViewQuadEntryName(view);
                return;
            }

            Transform parentTransform = GetOrCreateCategoryTransform(kViewQuadsCategoryName);

            GameObject entryObject = new GameObject(GetViewQuadDisplayName(view))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Transform entryTransform = entryObject.transform;
            entryTransform.SetParent(parentTransform, false);

            UnityAction handler = () => UpdateViewQuadEntryName(view);
            view.OnChanged.AddListener(handler);

            _viewQuadEntries[view] = new ViewQuadEntry(entryTransform, handler);
            _viewsByTransform[entryTransform] = view;
        }

        private void RemoveViewQuadEntry(View view)
        {
            if (view == null)
            {
                return;
            }

            if (!_viewQuadEntries.TryGetValue(view, out ViewQuadEntry entry))
            {
                return;
            }

            view.OnChanged.RemoveListener(entry.ViewChangedHandler);

            if (entry.Transform != null)
            {
                _viewsByTransform.Remove(entry.Transform);
                DestroyTransform(entry.Transform);
            }

            _viewQuadEntries.Remove(view);
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

                if (view != null && entry.ViewChangedHandler != null)
                {
                    view.OnChanged.RemoveListener(entry.ViewChangedHandler);
                }

                if (entry.Transform != null)
                {
                    _viewsByTransform.Remove(entry.Transform);
                    DestroyTransform(entry.Transform);
                }
            }

            _viewQuadEntries.Clear();
            _viewsByTransform.Clear();
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
                return "View Quad";
            }

            string viewQuadName = view.Data?.ViewQuad?.Name;
            string viewName = view.Name;

            if (!string.IsNullOrWhiteSpace(viewQuadName))
            {
                if (!string.IsNullOrWhiteSpace(viewName) &&
                    !string.Equals(viewQuadName, viewName, StringComparison.Ordinal))
                {
                    return $"{viewQuadName} ({viewName})";
                }

                return viewQuadName;
            }

            if (!string.IsNullOrWhiteSpace(viewName))
            {
                return $"{viewName} View Quad";
            }

            return "View Quad";
        }

        private void HighlightComponentInHierarchy(Component component)
        {
            if (_runtimeHierarchy == null || component == null)
            {
                return;
            }

            if (!_componentEntries.TryGetValue(component, out ComponentEntry entry) || entry.Transform == null)
            {
                return;
            }

            try
            {
                _suppressHierarchySelectionChange = true;
                _runtimeHierarchy.Select(entry.Transform, RuntimeHierarchy.SelectOptions.FocusOnSelection | RuntimeHierarchy.SelectOptions.ForceRevealSelection);
            }
            finally
            {
                _suppressHierarchySelectionChange = false;
            }
        }

        private void HighlightViewQuadInHierarchy(View view)
        {
            if (_runtimeHierarchy == null || view == null)
            {
                return;
            }

            if (!_viewQuadEntries.TryGetValue(view, out ViewQuadEntry entry) || entry.Transform == null)
            {
                return;
            }

            try
            {
                _suppressHierarchySelectionChange = true;
                _runtimeHierarchy.Select(entry.Transform, RuntimeHierarchy.SelectOptions.FocusOnSelection | RuntimeHierarchy.SelectOptions.ForceRevealSelection);
            }
            finally
            {
                _suppressHierarchySelectionChange = false;
            }
        }

        private void DeselectHierarchy()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            try
            {
                _suppressHierarchySelectionChange = true;
                _runtimeHierarchy.Deselect();
            }
            finally
            {
                _suppressHierarchySelectionChange = false;
            }
        }

        private void RefreshHierarchySelection()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            if (_currentViewQuadSelection != null)
            {
                HighlightViewQuadInHierarchy(_currentViewQuadSelection);
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController != null && selectionController.SelectedEditorComponents.Count > 0)
            {
                Oasis.LayoutEditor.EditorComponent editorComponent = selectionController.SelectedEditorComponents[0];
                if (editorComponent?.Component != null)
                {
                    HighlightComponentInHierarchy(editorComponent.Component);
                    return;
                }
            }

            DeselectHierarchy();
        }

        private void SubscribeToSelectionController()
        {
            if (_selectionEventsSubscribed)
            {
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            selectionController.OnSelectionChange.RemoveListener(OnSelectionControllerSelectionChanged);
            selectionController.OnSelectionChange.AddListener(OnSelectionControllerSelectionChanged);
            _selectionEventsSubscribed = true;
        }

        private void UnsubscribeFromSelectionController()
        {
            if (!_selectionEventsSubscribed)
            {
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController != null)
            {
                selectionController.OnSelectionChange.RemoveListener(OnSelectionControllerSelectionChanged);
            }

            _selectionEventsSubscribed = false;
        }

        private void SubscribeToInspectorEvents()
        {
            if (_inspectorEventsSubscribed)
            {
                return;
            }

            InspectorController inspector = Editor.Instance != null ? Editor.Instance.InspectorController : null;
            if (inspector == null)
            {
                return;
            }

            inspector.OnViewQuadSelectionChanged -= OnViewQuadSelectionChanged;
            inspector.OnViewQuadSelectionChanged += OnViewQuadSelectionChanged;
            _inspectorEventsSubscribed = true;
        }

        private void UnsubscribeFromInspectorEvents()
        {
            if (!_inspectorEventsSubscribed)
            {
                return;
            }

            InspectorController inspector = Editor.Instance != null ? Editor.Instance.InspectorController : null;
            if (inspector != null)
            {
                inspector.OnViewQuadSelectionChanged -= OnViewQuadSelectionChanged;
            }

            _inspectorEventsSubscribed = false;
        }

        private void SubscribeToRuntimeHierarchySelection()
        {
            if (_hierarchySelectionSubscribed || _runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
            _hierarchySelectionSubscribed = true;
        }

        private void UnsubscribeFromRuntimeHierarchySelection()
        {
            if (!_hierarchySelectionSubscribed || _runtimeHierarchy == null)
            {
                return;
            }

            _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            _hierarchySelectionSubscribed = false;
        }

        private void OnSelectionControllerSelectionChanged()
        {
            if (_suppressSelectionControllerEvents)
            {
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            if (selectionController.SelectedEditorComponents.Count == 0)
            {
                PanelViewQuadInspector viewQuadInspector = Editor.Instance?.InspectorController?.PanelViewQuadInspector;
                bool hasViewQuadSelection = viewQuadInspector != null && viewQuadInspector.HasTarget;

                if (!hasViewQuadSelection)
                {
                    _currentViewQuadSelection = null;
                    DeselectHierarchy();
                }

                return;
            }

            Oasis.LayoutEditor.EditorComponent editorComponent = selectionController.SelectedEditorComponents[0];
            if (editorComponent?.Component == null)
            {
                return;
            }

            _currentViewQuadSelection = null;
            HighlightComponentInHierarchy(editorComponent.Component);
        }

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            if (_suppressHierarchySelectionChange)
            {
                return;
            }

            if (selection == null || selection.Count == 0)
            {
                if (!_suppressSelectionControllerEvents)
                {
                    Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
                    if (selectionController != null && selectionController.SelectedEditorComponents.Count > 0)
                    {
                        _suppressSelectionControllerEvents = true;
                        selectionController.DeselectAllObjects();
                        _suppressSelectionControllerEvents = false;
                    }
                }

                if (_currentViewQuadSelection != null)
                {
                    BaseViewQuadOverlay overlay = GetOverlayForView(_currentViewQuadSelection);
                    if (overlay != null)
                    {
                        overlay.ClearHandleSelection();
                    }

                    _currentViewQuadSelection = null;
                }

                return;
            }

            Transform selectedTransform = selection[selection.Count - 1];
            if (selectedTransform != null && _componentsByTransform.TryGetValue(selectedTransform, out Component component) && component != null)
            {
                SelectComponentFromHierarchy(component);
                return;
            }

            if (selectedTransform != null && _viewsByTransform.TryGetValue(selectedTransform, out View view) && view != null)
            {
                SelectViewQuadFromHierarchy(view);
            }
        }

        private void OnViewQuadSelectionChanged(BaseViewQuadOverlay overlay, bool isSelected)
        {
            if (overlay == null)
            {
                return;
            }

            if (isSelected)
            {
                _currentViewQuadSelection = overlay.View;
                HighlightViewQuadInHierarchy(_currentViewQuadSelection);
            }
            else if (_currentViewQuadSelection != null && ReferenceEquals(_currentViewQuadSelection, overlay.View))
            {
                _currentViewQuadSelection = null;

                Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
                if (selectionController == null || selectionController.SelectedEditorComponents.Count == 0)
                {
                    DeselectHierarchy();
                }
                else
                {
                    Oasis.LayoutEditor.EditorComponent editorComponent = selectionController.SelectedEditorComponents[0];
                    if (editorComponent?.Component != null)
                    {
                        HighlightComponentInHierarchy(editorComponent.Component);
                    }
                }
            }
        }

        private void SelectComponentFromHierarchy(Component component)
        {
            if (component == null)
            {
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            View activeView = ResolveCurrentView();
            EditorView editorView = _currentEditorView ?? activeView?.EditorView;
            if (editorView == null)
            {
                return;
            }

            Oasis.LayoutEditor.EditorComponent editorComponent = editorView.GetEditorComponent(component);
            if (editorComponent == null)
            {
                return;
            }

            bool alreadySelected = selectionController.SelectedEditorComponents.Contains(editorComponent);

            _currentViewQuadSelection = null;

            if (alreadySelected && selectionController.SelectedEditorComponents.Count == 1)
            {
                HighlightComponentInHierarchy(component);
                return;
            }

            _suppressSelectionControllerEvents = true;
            selectionController.DeselectAllObjects();
            selectionController.SelectObject(editorComponent);
            _suppressSelectionControllerEvents = false;

            HighlightComponentInHierarchy(component);
        }

        private void SelectViewQuadFromHierarchy(View view)
        {
            if (view == null)
            {
                return;
            }

            BaseViewQuadOverlay overlay = GetOverlayForView(view);
            if (overlay == null)
            {
                return;
            }

            Oasis.LayoutEditor.SelectionController selectionController = Editor.Instance != null ? Editor.Instance.SelectionController : null;
            if (selectionController != null)
            {
                _suppressSelectionControllerEvents = true;
                selectionController.DeselectAllObjects();
                _suppressSelectionControllerEvents = false;
            }

            _currentViewQuadSelection = view;

            if (overlay.PointCount > 0)
            {
                int handleIndex = overlay.HasSelectedHandle ? overlay.SelectedHandleIndex : 0;
                handleIndex = Mathf.Clamp(handleIndex, 0, overlay.PointCount - 1);
                overlay.SelectHandle(handleIndex);
            }
            else
            {
                HighlightViewQuadInHierarchy(view);
            }
        }

        private BaseViewQuadOverlay GetOverlayForView(View view)
        {
            if (view == null)
            {
                return null;
            }

            InspectorController inspector = Editor.Instance != null ? Editor.Instance.InspectorController : null;
            if (inspector == null)
            {
                return null;
            }

            foreach (BaseViewQuadOverlay overlay in inspector.RegisteredViewQuadOverlays)
            {
                if (overlay != null && ReferenceEquals(overlay.View, view))
                {
                    return overlay;
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
