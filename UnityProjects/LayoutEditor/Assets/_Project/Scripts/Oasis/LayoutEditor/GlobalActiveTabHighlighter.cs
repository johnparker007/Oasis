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
            Outline outline = tab.gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = tab.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = _highlightColor;
            outline.effectDistance = new Vector2(_highlightThickness, _highlightThickness);
            outline.enabled = false;

            TabClickHandler clickHandler = tab.gameObject.GetComponent<TabClickHandler>();
            if (clickHandler == null)
            {
                clickHandler = tab.gameObject.AddComponent<TabClickHandler>();
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
                Outline currentOutline = _current.gameObject.GetComponent<Outline>();
                if (currentOutline != null)
                {
                    currentOutline.enabled = false;
                }
            }

            _current = tab;

            if (_current != null)
            {
                Outline newOutline = _current.gameObject.GetComponent<Outline>();
                if (newOutline == null)
                {
                    newOutline = _current.gameObject.AddComponent<Outline>();
                }

                newOutline.effectColor = _highlightColor;
                newOutline.effectDistance = new Vector2(_highlightThickness, _highlightThickness);
                newOutline.enabled = true;
            }
        }

        private void OnTabDestroyed(PanelTab tab)
        {
            if (_current == tab)
            {
                Outline outline = tab.gameObject.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
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
    }
}