using UnityEngine;

namespace Oasis.UI.Selection
{
    using UnityEngine;

    public class SelectionManager : MonoBehaviour
    {
        private const bool kDefaultIsSelecting = false;

        [Tooltip("Reference to the SelectionRectangleRenderer script.")]
        [SerializeField] private SelectionRectangleRenderer _selectionRenderer;

        [Tooltip("Reference to the Canvas.")]
        [SerializeField] private Canvas _canvas;

        public bool IsSelecting { get; private set; } = kDefaultIsSelecting;

        public Canvas Canvas
        {
            get
            {
                return _canvas;
            }
        }

        private Vector2 _startPosition;

        public void StartSelection(Vector2 startPos)
        {
            IsSelecting = true;
            _startPosition = startPos;
            _selectionRenderer.Show(startPos, startPos);
        }

        public void UpdateSelection(Vector2 currentPos)
        {
            if (IsSelecting)
            {
                _selectionRenderer.UpdateSelection(_startPosition, currentPos);
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