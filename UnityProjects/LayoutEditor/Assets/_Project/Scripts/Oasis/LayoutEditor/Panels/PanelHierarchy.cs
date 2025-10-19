using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicPanels;
using Oasis.Layout;
using Oasis.LayoutEditor;
using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        private bool _panelNotificationEventsSubscribed;
        private bool _suppressSelectionChangeHandling;
        private bool _suppressHierarchySelectionChange;
        private Coroutine _pendingActiveViewUpdate;
        private readonly Dictionary<EditorView, EditorViewFocusRelay> _viewFocusRelays = new();
        private readonly Dictionary<PanelTab, TabFocusRelay> _tabFocusRelays = new();

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
            EnsureTabFocusRelays();

            if (string.IsNullOrEmpty(_currentViewName))
            {
                _currentViewName = ViewController.kBaseViewName;
            }

            SubscribeToPanelNotificationEvents();
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
            UnsubscribeFromPanelNotificationEvents();

            ClearComponentEntries();
            ClearViewQuadEntries();
            DestroyCategoryRoots();
            DestroyViewQuadRoot();

            CancelPendingActiveViewUpdate();

            _currentView = null;
            _currentEditorView = null;
            _currentViewName = null;

            ClearFocusRelays();
            ClearTabFocusRelays();
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

        private void SubscribeToPanelNotificationEvents()
        {
            if (_panelNotificationEventsSubscribed)
            {
                return;
            }

            PanelNotificationCenter.OnActiveTabChanged += OnPanelActiveTabChanged;
            PanelNotificationCenter.OnPanelBecameActive += OnPanelBecameActive;
            PanelNotificationCenter.OnTabCreated += OnPanelTabCreated;
            PanelNotificationCenter.OnTabDestroyed += OnPanelTabDestroyed;
            _panelNotificationEventsSubscribed = true;
        }

        private void UnsubscribeFromPanelNotificationEvents()
        {
            if (!_panelNotificationEventsSubscribed)
            {
                return;
            }

            PanelNotificationCenter.OnActiveTabChanged -= OnPanelActiveTabChanged;
            PanelNotificationCenter.OnPanelBecameActive -= OnPanelBecameActive;
            PanelNotificationCenter.OnTabCreated -= OnPanelTabCreated;
            PanelNotificationCenter.OnTabDestroyed -= OnPanelTabDestroyed;
            _panelNotificationEventsSubscribed = false;
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

            EnsureFocusRelay(editorView);
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

            RemoveFocusRelay(editorView);
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

        private void OnPanelActiveTabChanged(PanelTab tab)
        {
            ScheduleActiveViewUpdate(tab);
        }

        private void OnPanelBecameActive(Panel panel)
        {
            if (panel == null)
            {
                return;
            }

            int activeIndex = panel.ActiveTab;
            if (activeIndex < 0)
            {
                return;
            }

            ScheduleActiveViewUpdate(panel[activeIndex]);
        }

        private void OnPanelTabCreated(PanelTab tab)
        {
            EnsureTabFocusRelay(tab);
        }

        private void OnPanelTabDestroyed(PanelTab tab)
        {
            RemoveTabFocusRelay(tab);
        }

        private static EditorView FindEditorViewForTab(PanelTab tab)
        {
            if (tab?.Content == null)
            {
                return null;
            }

            return tab.Content.GetComponentInChildren<EditorView>(true);
        }

        private void ScheduleActiveViewUpdate(PanelTab tab)
        {
            if (!isActiveAndEnabled || tab == null)
            {
                return;
            }

            CancelPendingActiveViewUpdate();
            _pendingActiveViewUpdate = StartCoroutine(ApplyActiveViewWhenReady(tab));
        }

        private IEnumerator ApplyActiveViewWhenReady(PanelTab tab)
        {
            yield return null;

            ApplyActiveTab(tab);
            _pendingActiveViewUpdate = null;
        }

        private void ApplyActiveTab(PanelTab tab)
        {
            if (!isActiveAndEnabled || tab == null)
            {
                return;
            }

            LayoutObject layout = Editor.Instance?.Project?.Layout;
            EditorView editorView = FindEditorViewForTab(tab);

            string viewName = editorView != null ? editorView.ViewName : null;
            View resolvedView = null;

            if (layout != null && !string.IsNullOrEmpty(viewName))
            {
                resolvedView = layout.GetView(viewName);
            }

            string labelName = tab.Label;
            if ((resolvedView == null || string.IsNullOrEmpty(viewName)) && !string.IsNullOrEmpty(labelName))
            {
                View labelView = layout?.GetView(labelName);
                if (labelView != null)
                {
                    resolvedView = labelView;
                    viewName = labelView.Name;
                }
                else if (string.IsNullOrEmpty(viewName))
                {
                    viewName = labelName;
                }
            }

            if (editorView == null && !string.IsNullOrEmpty(viewName))
            {
                editorView = ViewController.GetEditorView(viewName);
            }

            if (editorView != null)
            {
                EnsureFocusRelay(editorView);
                _currentEditorView = editorView;
                if (resolvedView == null && layout != null)
                {
                    resolvedView = layout.GetView(editorView.ViewName);
                }
            }
            else if (resolvedView == null)
            {
                return;
            }
            else
            {
                _currentEditorView = null;
            }

            if (resolvedView == null && layout != null && !string.IsNullOrEmpty(viewName))
            {
                resolvedView = layout.GetView(viewName);
            }

            if (!string.IsNullOrEmpty(viewName))
            {
                _currentViewName = viewName;
            }

            SetCurrentView(resolvedView);
        }

        private void HandleEditorViewFocused(EditorView editorView)
        {
            if (!isActiveAndEnabled || editorView == null)
            {
                return;
            }

            _currentEditorView = editorView;

            if (!string.IsNullOrEmpty(editorView.ViewName))
            {
                _currentViewName = editorView.ViewName;
            }

            SetCurrentView(ResolveView(editorView));
        }

        private void HandleTabFocused(PanelTab tab)
        {
            if (!isActiveAndEnabled || tab == null)
            {
                return;
            }

            ScheduleActiveViewUpdate(tab);
        }

        private void EnsureTabFocusRelays()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            foreach (PanelTab tab in Resources.FindObjectsOfTypeAll<PanelTab>())
            {
                if (tab == null || !tab.gameObject.scene.IsValid())
                {
                    continue;
                }

                EnsureTabFocusRelay(tab);
            }
        }

        private void EnsureTabFocusRelay(PanelTab tab)
        {
            if (tab == null)
            {
                return;
            }

            if (_tabFocusRelays.TryGetValue(tab, out TabFocusRelay existingRelay) && existingRelay != null)
            {
                existingRelay.Initialize(this, tab);
                return;
            }

            TabFocusRelay relay = tab.GetComponent<TabFocusRelay>();
            if (relay == null)
            {
                relay = tab.gameObject.AddComponent<TabFocusRelay>();
            }

            relay.Initialize(this, tab);
            _tabFocusRelays[tab] = relay;
        }

        private void RemoveTabFocusRelay(PanelTab tab)
        {
            if (tab == null)
            {
                return;
            }

            if (_tabFocusRelays.TryGetValue(tab, out TabFocusRelay relay))
            {
                if (relay != null)
                {
                    relay.Release(this);
                }

                _tabFocusRelays.Remove(tab);
            }
        }

        private void ClearTabFocusRelays()
        {
            if (_tabFocusRelays.Count == 0)
            {
                return;
            }

            foreach (TabFocusRelay relay in _tabFocusRelays.Values)
            {
                relay?.Release(this);
            }

            _tabFocusRelays.Clear();
        }

        private void EnsureFocusRelay(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            if (_viewFocusRelays.TryGetValue(editorView, out EditorViewFocusRelay existingRelay) && existingRelay != null)
            {
                existingRelay.Initialize(this, editorView);
                return;
            }

            EditorPanel editorPanel = editorView.GetComponentInChildren<EditorPanel>(true);
            if (editorPanel == null)
            {
                return;
            }

            EditorViewFocusRelay relay = editorPanel.GetComponent<EditorViewFocusRelay>();
            if (relay == null)
            {
                relay = editorPanel.gameObject.AddComponent<EditorViewFocusRelay>();
            }

            relay.Initialize(this, editorView);
            _viewFocusRelays[editorView] = relay;
        }

        private void RemoveFocusRelay(EditorView editorView)
        {
            if (editorView == null)
            {
                return;
            }

            if (_viewFocusRelays.TryGetValue(editorView, out EditorViewFocusRelay relay))
            {
                if (relay != null)
                {
                    relay.Release(this);
                }

                _viewFocusRelays.Remove(editorView);
            }
        }

        private void ClearFocusRelays()
        {
            if (_viewFocusRelays.Count == 0)
            {
                return;
            }

            foreach (EditorViewFocusRelay relay in _viewFocusRelays.Values)
            {
                relay?.Release(this);
            }

            _viewFocusRelays.Clear();
        }

        private sealed class TabFocusRelay : MonoBehaviour, IPointerDownHandler
        {
            private readonly List<TabPointerHandler> _pointerHandlers = new();
            private readonly List<TabContentWatcher> _hierarchyWatchers = new();
            private readonly HashSet<Transform> _registeredTransforms = new();

            private PanelHierarchy _hierarchy;
            private PanelTab _tab;

            public void Initialize(PanelHierarchy hierarchy, PanelTab tab)
            {
                bool hierarchyChanged = !ReferenceEquals(_hierarchy, hierarchy);
                bool tabChanged = !ReferenceEquals(_tab, tab);

                if (hierarchyChanged || tabChanged)
                {
                    ReleaseHandlers();
                    _hierarchy = hierarchy;
                    _tab = tab;
                }

                AttachHandlers();
            }

            public void Release(PanelHierarchy hierarchy)
            {
                if (!ReferenceEquals(_hierarchy, hierarchy))
                {
                    return;
                }

                ReleaseHandlers();

                _hierarchy = null;
                _tab = null;

                Destroy(this);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                NotifyFocusRequested();
            }

            private void AttachHandlers()
            {
                if (_hierarchy == null || _tab == null)
                {
                    return;
                }

                RegisterPointerHandler(_tab.gameObject);

                if (_tab.Content != null)
                {
                    AddHandlersRecursively(_tab.Content);
                }
            }

            private void ReleaseHandlers()
            {
                if (_pointerHandlers.Count == 0 && _hierarchyWatchers.Count == 0)
                {
                    _registeredTransforms.Clear();
                    return;
                }

                foreach (TabPointerHandler handler in _pointerHandlers)
                {
                    handler?.Release(this);
                }

                foreach (TabContentWatcher watcher in _hierarchyWatchers)
                {
                    watcher?.Release(this);
                }

                _pointerHandlers.Clear();
                _hierarchyWatchers.Clear();
                _registeredTransforms.Clear();
            }

            private void RegisterPointerHandler(GameObject target)
            {
                if (target == null)
                {
                    return;
                }

                TabPointerHandler handler = target.GetComponent<TabPointerHandler>();
                if (handler == null)
                {
                    handler = target.AddComponent<TabPointerHandler>();
                }

                handler.Initialize(this);
                _pointerHandlers.Add(handler);
            }

            private void AddHandlersRecursively(Transform root)
            {
                if (root == null)
                {
                    return;
                }

                bool isNewTransform = _registeredTransforms.Add(root);
                if (isNewTransform)
                {
                    RegisterPointerHandler(root.gameObject);

                    TabContentWatcher watcher = root.GetComponent<TabContentWatcher>();
                    if (watcher == null)
                    {
                        watcher = root.gameObject.AddComponent<TabContentWatcher>();
                    }

                    watcher.Initialize(this);
                    _hierarchyWatchers.Add(watcher);
                }

                foreach (Transform child in root)
                {
                    AddHandlersRecursively(child);
                }
            }

            private void NotifyFocusRequested()
            {
                if (_hierarchy == null || _tab == null)
                {
                    return;
                }

                _hierarchy.HandleTabFocused(_tab);
            }

            private void HandleChildrenChanged(Transform root)
            {
                if (root == null)
                {
                    return;
                }

                foreach (Transform child in root)
                {
                    AddHandlersRecursively(child);
                }
            }

            private sealed class TabPointerHandler : MonoBehaviour, IPointerDownHandler
            {
                private TabFocusRelay _relay;

                public void Initialize(TabFocusRelay relay)
                {
                    _relay = relay;
                }

                public void Release(TabFocusRelay relay)
                {
                    if (!ReferenceEquals(_relay, relay))
                    {
                        return;
                    }

                    _relay = null;
                    Destroy(this);
                }

                public void OnPointerDown(PointerEventData eventData)
                {
                    _relay?.NotifyFocusRequested();
                }
            }

            private sealed class TabContentWatcher : MonoBehaviour
            {
                private TabFocusRelay _relay;

                public void Initialize(TabFocusRelay relay)
                {
                    _relay = relay;
                }

                public void Release(TabFocusRelay relay)
                {
                    if (!ReferenceEquals(_relay, relay))
                    {
                        return;
                    }

                    _relay = null;
                    Destroy(this);
                }

                private void OnTransformChildrenChanged()
                {
                    _relay?.HandleChildrenChanged(transform);
                }
            }
        }

        private sealed class EditorViewFocusRelay : MonoBehaviour, IPointerDownHandler
        {
            private readonly List<FocusForwarder> _focusForwarders = new();
            private readonly List<ContentHierarchyWatcher> _hierarchyWatchers = new();
            private readonly HashSet<Transform> _registeredTransforms = new();

            private PanelHierarchy _hierarchy;
            private EditorView _editorView;

            public void Initialize(PanelHierarchy hierarchy, EditorView editorView)
            {
                bool hierarchyChanged = !ReferenceEquals(_hierarchy, hierarchy);
                bool viewChanged = !ReferenceEquals(_editorView, editorView);

                if (hierarchyChanged || viewChanged)
                {
                    ReleaseHandlers();
                    _hierarchy = hierarchy;
                    _editorView = editorView;
                }

                AttachHandlers();
            }

            public void Release(PanelHierarchy hierarchy)
            {
                if (!ReferenceEquals(_hierarchy, hierarchy))
                {
                    return;
                }

                ReleaseHandlers();

                _hierarchy = null;
                _editorView = null;

                Destroy(this);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                NotifyFocusRequested();
            }

            private void AttachHandlers()
            {
                if (_hierarchy == null || _editorView == null)
                {
                    return;
                }

                AddHandlersRecursively(_editorView.transform);

                GraphicRaycaster contentRaycaster = _editorView.Content;
                if (contentRaycaster != null)
                {
                    AddHandlersRecursively(contentRaycaster.transform);
                }
            }

            private void ReleaseHandlers()
            {
                if (_focusForwarders.Count == 0 && _hierarchyWatchers.Count == 0)
                {
                    _registeredTransforms.Clear();
                    return;
                }

                foreach (FocusForwarder forwarder in _focusForwarders)
                {
                    forwarder?.Release(this);
                }

                foreach (ContentHierarchyWatcher watcher in _hierarchyWatchers)
                {
                    watcher?.Release(this);
                }

                _focusForwarders.Clear();
                _hierarchyWatchers.Clear();
                _registeredTransforms.Clear();
            }

            private void AddHandlersRecursively(Transform root)
            {
                if (root == null)
                {
                    return;
                }

                bool isNewTransform = _registeredTransforms.Add(root);
                if (isNewTransform)
                {
                    FocusForwarder forwarder = root.GetComponent<FocusForwarder>();
                    if (forwarder == null)
                    {
                        forwarder = root.gameObject.AddComponent<FocusForwarder>();
                    }
                    forwarder.Initialize(this);
                    _focusForwarders.Add(forwarder);

                    ContentHierarchyWatcher watcher = root.GetComponent<ContentHierarchyWatcher>();
                    if (watcher == null)
                    {
                        watcher = root.gameObject.AddComponent<ContentHierarchyWatcher>();
                    }
                    watcher.Initialize(this);
                    _hierarchyWatchers.Add(watcher);
                }

                foreach (Transform child in root)
                {
                    AddHandlersRecursively(child);
                }
            }

            private void HandleChildrenChanged(Transform root)
            {
                if (root == null)
                {
                    return;
                }

                foreach (Transform child in root)
                {
                    AddHandlersRecursively(child);
                }
            }

            private void NotifyFocusRequested()
            {
                if (_hierarchy == null || _editorView == null)
                {
                    return;
                }

                _hierarchy.HandleEditorViewFocused(_editorView);
            }

            private sealed class FocusForwarder : MonoBehaviour, IPointerDownHandler
            {
                private EditorViewFocusRelay _relay;

                public void Initialize(EditorViewFocusRelay relay)
                {
                    _relay = relay;
                }

                public void Release(EditorViewFocusRelay relay)
                {
                    if (!ReferenceEquals(_relay, relay))
                    {
                        return;
                    }

                    _relay = null;
                    Destroy(this);
                }

                public void OnPointerDown(PointerEventData eventData)
                {
                    _relay?.NotifyFocusRequested();
                }
            }

            private sealed class ContentHierarchyWatcher : MonoBehaviour
            {
                private EditorViewFocusRelay _relay;

                public void Initialize(EditorViewFocusRelay relay)
                {
                    _relay = relay;
                }

                public void Release(EditorViewFocusRelay relay)
                {
                    if (!ReferenceEquals(_relay, relay))
                    {
                        return;
                    }

                    _relay = null;
                    Destroy(this);
                }

                private void OnTransformChildrenChanged()
                {
                    _relay?.HandleChildrenChanged(transform);
                }
            }
        }

        private void CancelPendingActiveViewUpdate()
        {
            if (_pendingActiveViewUpdate != null)
            {
                StopCoroutine(_pendingActiveViewUpdate);
                _pendingActiveViewUpdate = null;
            }
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
