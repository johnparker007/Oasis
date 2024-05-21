using RuntimeInspectorNamespace;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelBase : SkinnedWindow
    {
        [Header("Internal Variables")]
        [SerializeField]
        private ScrollRect _scrollView;
        private RectTransform _drawArea;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private Image _scrollbar;

        // Used to make sure that the scrolled content always remains within the scroll view's boundaries
        private PointerEventData _nullPointerEventData;

        private bool _initialized = false;

        private Canvas _canvas;

        public Canvas Canvas => _canvas;

        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }


		private void Initialize()
		{
			if (_initialized)
				return;

			_initialized = true;

			_drawArea = _scrollView.content;
			_canvas = GetComponentInParent<Canvas>();
			_nullPointerEventData = new PointerEventData(null);

			//if (m_showTooltips)
			//{
			//	TooltipListener = gameObject.AddComponent<TooltipListener>();
			//	TooltipListener.Initialize(this);
			//}

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			// On new Input System, scroll sensitivity is much higher than legacy Input system
			scrollView.scrollSensitivity *= 0.25f;
#endif
		}

		protected override void RefreshSkin()
        {
            _background.color = Skin.BackgroundColor;
            _scrollbar.color = Skin.ScrollbarColor;

            //if (IsBound && !isDirty)
            //    currentDrawer.Skin = Skin;
        }

        // Makes sure that scroll view's contents are within scroll view's bounds
        internal void EnsureScrollViewIsWithinBounds()
        {
            // When scrollbar is snapped to the very bottom of the scroll view, sometimes OnScroll alone doesn't work
            if (_scrollView.verticalNormalizedPosition <= Mathf.Epsilon)
                _scrollView.verticalNormalizedPosition = 0.0001f;

            _scrollView.OnScroll(_nullPointerEventData);
        }

        private void OnTransformParentChanged()
        {
            _canvas = GetComponentInParent<Canvas>();
        }
    }

}
