using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    [DisallowMultipleComponent]
    internal sealed class EditorViewSelectionOverlay : MonoBehaviour
    {
        private const float kLineThickness = 2f;

        private static readonly Vector3[] s_worldCorners = new Vector3[4];
        private static readonly Vector2[] s_localCorners = new Vector2[4];

        private readonly Dictionary<EditorComponent, OutlineEntry> _entries = new();
        private readonly List<EditorComponent> _activeComponents = new();

        private EditorView _editorView;
        private RectTransform _contentRect;
        private RectTransform _rectTransform;
        private Zoom _zoom;
        private SelectionController _selectionController;
        private Vector2 _lastObservedContentSize = Vector2.zero;
        private bool _initialised;

        public void Initialize(
            EditorView editorView,
            RectTransform contentRect,
            Zoom zoom,
            SelectionController selectionController)
        {
            _editorView = editorView;
            _contentRect = contentRect;
            _zoom = zoom;
            _selectionController = selectionController;
            _rectTransform = GetComponent<RectTransform>();

            ConfigureRectTransform();

            if (_selectionController != null)
            {
                _selectionController.OnSelectionChange.AddListener(OnSelectionChanged);
                _selectionController.OnSelectionMove.AddListener(OnSelectionMoved);
            }

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.AddListener(OnZoomLevelChanged);
            }

            _initialised = true;
            RefreshOutlines();
        }

        private void ConfigureRectTransform()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (_rectTransform == null)
            {
                return;
            }

            _rectTransform.anchorMin = new Vector2(0f, 1f);
            _rectTransform.anchorMax = new Vector2(0f, 1f);
            _rectTransform.pivot = new Vector2(0f, 1f);
            _rectTransform.anchoredPosition = Vector2.zero;

            UpdateOverlaySize();
        }

        private void LateUpdate()
        {
            if (!_initialised)
            {
                return;
            }

            if (UpdateOverlaySize() || _activeComponents.Count > 0)
            {
                UpdateActiveOutlines();
            }

            if (_selectionController == null && Editor.Instance != null)
            {
                SelectionController controller = Editor.Instance.SelectionController;
                if (controller != null)
                {
                    _selectionController = controller;
                    _selectionController.OnSelectionChange.AddListener(OnSelectionChanged);
                    _selectionController.OnSelectionMove.AddListener(OnSelectionMoved);
                    RefreshOutlines();
                }
            }
        }

        private void OnDestroy()
        {
            if (_selectionController != null)
            {
                _selectionController.OnSelectionChange.RemoveListener(OnSelectionChanged);
                _selectionController.OnSelectionMove.RemoveListener(OnSelectionMoved);
            }

            if (_zoom != null)
            {
                _zoom.OnZoomLevelSet.RemoveListener(OnZoomLevelChanged);
            }

            ClearAllOutlines();
        }

        private void OnSelectionChanged()
        {
            RefreshOutlines();
        }

        private void OnSelectionMoved()
        {
            UpdateActiveOutlines();
        }

        private void OnZoomLevelChanged(float zoomLevel)
        {
            UpdateActiveOutlines();
        }

        private void RefreshOutlines()
        {
            if (!_initialised)
            {
                return;
            }

            if (_selectionController == null)
            {
                DeactivateAllOutlines();
                return;
            }

            UpdateOverlaySize();

            IReadOnlyList<EditorComponent> selectedComponents =
                _selectionController.SelectedEditorComponents;

            HashSet<EditorComponent> requiredComponents = new();
            _activeComponents.Clear();

            if (selectedComponents != null)
            {
                for (int i = 0; i < selectedComponents.Count; i++)
                {
                    EditorComponent editorComponent = selectedComponents[i];
                    if (!BelongsToCurrentView(editorComponent))
                    {
                        continue;
                    }

                    requiredComponents.Add(editorComponent);
                    _activeComponents.Add(editorComponent);

                    OutlineEntry entry = GetOrCreateEntry(editorComponent);
                    entry.SetActive(true);
                    UpdateEntry(editorComponent, entry);
                }
            }

            List<EditorComponent> keysToRemove = null;
            foreach (KeyValuePair<EditorComponent, OutlineEntry> pair in _entries)
            {
                EditorComponent editorComponent = pair.Key;
                OutlineEntry entry = pair.Value;

                if (requiredComponents.Contains(editorComponent))
                {
                    continue;
                }

                entry.SetActive(false);

                if (editorComponent == null || !BelongsToCurrentView(editorComponent))
                {
                    keysToRemove ??= new List<EditorComponent>();
                    keysToRemove.Add(editorComponent);
                }
            }

            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    EditorComponent component = keysToRemove[i];
                    if (_entries.TryGetValue(component, out OutlineEntry entry))
                    {
                        entry.Destroy();
                    }

                    _entries.Remove(component);
                }
            }
        }

        private void UpdateActiveOutlines()
        {
            if (_activeComponents.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _activeComponents.Count; i++)
            {
                EditorComponent editorComponent = _activeComponents[i];
                if (editorComponent == null)
                {
                    continue;
                }

                if (_entries.TryGetValue(editorComponent, out OutlineEntry entry))
                {
                    entry.SetActive(true);
                    UpdateEntry(editorComponent, entry);
                }
            }
        }

        private OutlineEntry GetOrCreateEntry(EditorComponent editorComponent)
        {
            if (_entries.TryGetValue(editorComponent, out OutlineEntry existingEntry))
            {
                return existingEntry;
            }

            RectTransform[] edges = new RectTransform[4];

            for (int i = 0; i < edges.Length; i++)
            {
                GameObject edgeObject = new GameObject(
                    $"SelectionEdge{i}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

                edgeObject.transform.SetParent(transform, false);

                RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
                edgeRect.anchorMin = new Vector2(0f, 1f);
                edgeRect.anchorMax = new Vector2(0f, 1f);
                edgeRect.pivot = new Vector2(0f, 0.5f);
                edgeRect.anchoredPosition = Vector2.zero;
                edgeRect.sizeDelta = Vector2.zero;

                Image edgeImage = edgeObject.GetComponent<Image>();
                edgeImage.color = Color.white;
                edgeImage.raycastTarget = false;

                edges[i] = edgeRect;
            }

            OutlineEntry entry = new OutlineEntry(edges);
            entry.SetActive(false);
            _entries[editorComponent] = entry;

            return entry;
        }

        private bool BelongsToCurrentView(EditorComponent editorComponent)
        {
            if (editorComponent == null)
            {
                return false;
            }

            if (_contentRect == null)
            {
                return false;
            }

            Transform componentTransform = editorComponent.transform;
            if (componentTransform == null)
            {
                return false;
            }

            return componentTransform.IsChildOf(_contentRect);
        }

        private void UpdateEntry(EditorComponent editorComponent, OutlineEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            RectTransform componentRect = editorComponent != null
                ? editorComponent.GetComponent<RectTransform>()
                : null;

            if (componentRect == null)
            {
                entry.SetActive(false);
                return;
            }

            componentRect.GetWorldCorners(s_worldCorners);
            for (int i = 0; i < s_worldCorners.Length; i++)
            {
                Vector3 localCorner = _rectTransform.InverseTransformPoint(s_worldCorners[i]);
                s_localCorners[i] = new Vector2(localCorner.x, localCorner.y);
            }

            RectTransform[] edges = entry.Edges;
            if (edges == null || edges.Length < 4)
            {
                return;
            }

            Vector2 topLeft = s_localCorners[1];
            Vector2 topRight = s_localCorners[2];
            Vector2 bottomRight = s_localCorners[3];
            Vector2 bottomLeft = s_localCorners[0];

            SetEdge(edges[0], topLeft, topRight);
            SetEdge(edges[1], topRight, bottomRight);
            SetEdge(edges[2], bottomRight, bottomLeft);
            SetEdge(edges[3], bottomLeft, topLeft);
        }

        private void SetEdge(RectTransform edge, Vector2 start, Vector2 end)
        {
            if (edge == null)
            {
                return;
            }

            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (Mathf.Approximately(length, 0f))
            {
                edge.gameObject.SetActive(false);
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float lineThickness = GetLineThickness();

            edge.gameObject.SetActive(true);
            edge.anchoredPosition = start;
            edge.sizeDelta = new Vector2(length, lineThickness);
            edge.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private float GetLineThickness()
        {
            float zoomLevel = _zoom != null ? Mathf.Max(_zoom.ZoomLevel, 0.0001f) : 1f;
            return kLineThickness / zoomLevel;
        }

        private bool UpdateOverlaySize()
        {
            if (_rectTransform == null || _contentRect == null)
            {
                return false;
            }

            Vector2 size = _contentRect.rect.size;
            if (Mathf.Approximately(size.x, 0f) && Mathf.Approximately(size.y, 0f))
            {
                size = _contentRect.sizeDelta;
            }

            if (Approximately(size, _lastObservedContentSize))
            {
                return false;
            }

            _rectTransform.sizeDelta = size;
            _lastObservedContentSize = size;
            return true;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private void DeactivateAllOutlines()
        {
            foreach (KeyValuePair<EditorComponent, OutlineEntry> pair in _entries)
            {
                pair.Value.SetActive(false);
            }

            _activeComponents.Clear();
        }

        private void ClearAllOutlines()
        {
            foreach (KeyValuePair<EditorComponent, OutlineEntry> pair in _entries)
            {
                pair.Value.Destroy();
            }

            _entries.Clear();
            _activeComponents.Clear();
        }

        private sealed class OutlineEntry
        {
            public OutlineEntry(RectTransform[] edges)
            {
                Edges = edges;
            }

            public RectTransform[] Edges { get; }

            public void SetActive(bool active)
            {
                if (Edges == null)
                {
                    return;
                }

                for (int i = 0; i < Edges.Length; i++)
                {
                    RectTransform edge = Edges[i];
                    if (edge == null)
                    {
                        continue;
                    }

                    GameObject edgeObject = edge.gameObject;
                    if (edgeObject != null)
                    {
                        edgeObject.SetActive(active);
                    }
                }
            }

            public void Destroy()
            {
                if (Edges == null)
                {
                    return;
                }

                for (int i = 0; i < Edges.Length; i++)
                {
                    RectTransform edge = Edges[i];
                    if (edge == null)
                    {
                        continue;
                    }

                    GameObject edgeObject = edge.gameObject;
                    if (edgeObject == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Destroy(edgeObject);
                    }
                    else
                    {
                        DestroyImmediate(edgeObject);
                    }
                }
            }
        }
    }
}

