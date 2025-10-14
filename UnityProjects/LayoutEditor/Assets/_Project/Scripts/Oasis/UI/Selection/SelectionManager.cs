using UnityEngine;

namespace Oasis.UI.Selection
{
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    public class SelectionManager : MonoBehaviour
    {
        [Tooltip("Reference to the SelectionRectangleRenderer script.")]
        [SerializeField] private SelectionRectangleRenderer _selectionRenderer;

        [Tooltip("Reference to the ComponentSelector script.")]
        [SerializeField] private ComponentSelector _componentSelector;

        [Tooltip("Reference to the Canvas.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("Reference to the ScrollRect.")]
        [SerializeField] private ScrollRect _scrollRect;

        public UnityAction OnSelectStart;
        public UnityAction OnSelectEnd;


        public bool IsSelecting 
        { 
            get
            {
                return _isSelecting;
            }
            private set
            {
                if(_isSelecting != value)
                {
                    if(value)
                    {
                        OnSelectStart?.Invoke();
                    }
                    else
                    {
                        OnSelectEnd?.Invoke();
                    }
                }

                _isSelecting = value;
            }
        } 

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

        public Vector2 StartPosition
        {
            get;
            private set;
        } = Vector2.zero;

        public Vector2 CurrentPosition
        {
            get;
            private set;
        } = Vector2.zero;

        private bool _isSelecting = false;


        public void StartSelection(Vector2 startPosition)
        {
            IsSelecting = true;

            startPosition.x += _scrollRect.content.localPosition.x;
            startPosition.y += _scrollRect.content.localPosition.y;

            StartPosition = startPosition;

            _selectionRenderer.Show(StartPosition, StartPosition);
        }

        public void UpdateSelection(Vector2 currentPosition)
        {
            if (IsSelecting)
            {
                currentPosition.x += _scrollRect.content.localPosition.x;
                currentPosition.y += _scrollRect.content.localPosition.y;

                CurrentPosition = currentPosition;

                _selectionRenderer.UpdateSelection(StartPosition, CurrentPosition);
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