using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DynamicPanels;

namespace Oasis.LayoutEditor
{
    public class GlobalActiveTabHighlighter : MonoBehaviour
    {
        private const float kDefaultHighlightThickness = 2f;

        [SerializeField]
        private Color _highlightColor = Color.yellow;

        [SerializeField]
        private float _highlightThickness = kDefaultHighlightThickness;

        private PanelTab _current;

        private void OnEnable()
        {
            PanelNotificationCenter.OnActiveTabChanged += OnActiveTabChanged;
            PanelNotificationCenter.OnTabCreated += OnTabCreated;
            PanelNotificationCenter.OnTabDestroyed += OnTabDestroyed;
        }

        private void OnDisable()
        {
            PanelNotificationCenter.OnActiveTabChanged -= OnActiveTabChanged;
            PanelNotificationCenter.OnTabCreated -= OnTabCreated;
            PanelNotificationCenter.OnTabDestroyed -= OnTabDestroyed;
        }

        private void OnTabCreated(PanelTab tab)
        {
            TabHighlight highlight = tab.gameObject.GetComponent<TabHighlight>();
            if (highlight == null)
            {
                highlight = tab.gameObject.AddComponent<TabHighlight>();
            }

            highlight.color = _highlightColor;
            highlight.Thickness = _highlightThickness;
            highlight.enabled = false;

            AddClickHandler(tab.gameObject, tab);

            if (tab.Content != null)
            {
                // Add handlers for all existing descendants of the tab content
                AddClickHandlersRecursively(tab.Content, tab);

                // Watch for future hierarchy changes so dynamically created children
                // also receive click handlers
                ContentHierarchyWatcher watcher = tab.Content.gameObject.GetComponent<ContentHierarchyWatcher>();
                if (watcher == null)
                {
                    watcher = tab.Content.gameObject.AddComponent<ContentHierarchyWatcher>();
                }
                watcher.Initialize(this, tab);
            }
        }

        private void AddClickHandlersRecursively(Transform root, PanelTab tab)
        {
            AddClickHandler(root.gameObject, tab);

            foreach (Transform child in root)
            {
                AddClickHandlersRecursively(child, tab);
            }
        }

        private void AddClickHandler(GameObject target, PanelTab tab)
        {
            TabClickHandler clickHandler = target.GetComponent<TabClickHandler>();
            if (clickHandler == null)
            {
                clickHandler = target.AddComponent<TabClickHandler>();
            }
            clickHandler.Initialize(this, tab);
        }

        private void OnActiveTabChanged(PanelTab tab)
        {
            HighlightTab(tab);
        }

        public void HighlightTab(PanelTab tab)
        {
            if (_current != null)
            {
                TabHighlight currentHighlight = _current.gameObject.GetComponent<TabHighlight>();
                if (currentHighlight != null)
                {
                    currentHighlight.enabled = false;
                }
            }

            _current = tab;

            if (_current != null)
            {
                TabHighlight newHighlight = _current.gameObject.GetComponent<TabHighlight>();
                if (newHighlight == null)
                {
                    newHighlight = _current.gameObject.AddComponent<TabHighlight>();
                }

                newHighlight.color = _highlightColor;
                newHighlight.Thickness = _highlightThickness;
                newHighlight.enabled = true;
            }
        }

        private void OnTabDestroyed(PanelTab tab)
        {
            if (_current == tab)
            {
                TabHighlight highlight = tab.gameObject.GetComponent<TabHighlight>();
                if (highlight != null)
                {
                    highlight.enabled = false;
                }

                _current = null;
            }
        }
        private sealed class TabClickHandler : MonoBehaviour, IPointerDownHandler
        {
            private GlobalActiveTabHighlighter _highlighter;
            private PanelTab _tab;

            public void Initialize(GlobalActiveTabHighlighter highlighter, PanelTab tab)
            {
                _highlighter = highlighter;
                _tab = tab;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (_highlighter != null)
                {
                    _highlighter.HighlightTab(_tab);
                }
            }
        }

        private sealed class ContentHierarchyWatcher : MonoBehaviour
        {
            private GlobalActiveTabHighlighter _highlighter;
            private PanelTab _tab;

            public void Initialize(GlobalActiveTabHighlighter highlighter, PanelTab tab)
            {
                _highlighter = highlighter;
                _tab = tab;
            }

            private void OnTransformChildrenChanged()
            {
                if (_highlighter != null)
                {
                    _highlighter.AddClickHandlersRecursively(transform, _tab);
                }
            }
        }
    }
}
