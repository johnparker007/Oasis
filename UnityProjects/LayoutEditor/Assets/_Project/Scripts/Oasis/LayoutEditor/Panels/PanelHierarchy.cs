using System;
using System.Collections.Generic;
using System.Linq;
using Oasis.Layout;
using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using UnityEngine;
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

        [SerializeField]
        private RuntimeHierarchy _runtimeHierarchy = null;

        private RuntimeHierarchyStandaloneTransformCollection _transformCollection;
        private readonly Dictionary<string, Transform> _categoryRoots = new(StringComparer.Ordinal);
        private readonly Dictionary<Component, ComponentEntry> _componentEntries = new();

        private LayoutObject _observedLayout;
        private View _currentView;
        private string _currentViewName;
        private EditorView _currentEditorView;
        private bool _eventsSubscribed;

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

            RefreshActiveView();
        }

        private void OnDisable()
        {
            UnsubscribeFromLayout();
            UnsubscribeFromEditorEvents();

            ClearComponentEntries();
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
            EnsureCategoryRoots();

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
    }
}
