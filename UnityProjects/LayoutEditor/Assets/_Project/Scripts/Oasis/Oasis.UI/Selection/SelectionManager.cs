using UnityEngine;

namespace Oasis.UI.Selection
{
    using UnityEngine;
    using UnityEngine.UI;

    public class SelectionManager : MonoBehaviour
    {
        private const bool kDefaultIsSelecting = false;

        [Tooltip("Reference to the SelectionRectangleRenderer script.")]
        [SerializeField] private SelectionRectangleRenderer _selectionRenderer;

        [Tooltip("Reference to the Canvas.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("Reference to the ScrollRect.")]
        [SerializeField] private ScrollRect _scrollRect;

        public bool IsSelecting { get; private set; } = kDefaultIsSelecting;

        public Canvas Canvas
        {
            get
            {
                return _canvas;
            }
        }

        public float ContentZoom
        {
            get
            {
                // x & y scale will always be locked to the same value so can safely simply return x
                return _scrollRect.content.localScale.x;
            }
        }

        private Vector2 _startPosition = Vector2.zero;


        public void StartSelection(Vector2 startPosition)
        {
            IsSelecting = true;
            _startPosition = startPosition;

            _startPosition.x += _scrollRect.content.localPosition.x;
            _startPosition.y += _scrollRect.content.localPosition.y;

            _selectionRenderer.Show(_startPosition, _startPosition);
        }

        public void UpdateSelection(Vector2 currentPosition)
        {
            if (IsSelecting)
            {
                currentPosition.x += _scrollRect.content.localPosition.x;
                currentPosition.y += _scrollRect.content.localPosition.y;

                _selectionRenderer.UpdateSelection(_startPosition, currentPosition);
            }
        }

        public void EndSelection()
        {
            if (IsSelecting)
            {
                IsSelecting = false;
                _selectionRenderer.Hide();
            }
        }
    }

}