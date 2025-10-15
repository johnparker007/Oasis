using Oasis.Layout;
using Oasis.UI;
using Oasis.UI.Fields;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelViewQuadInspector : PanelBase
    {
        [SerializeField]
        private FieldVector2 _layoutPoint;

        private BaseViewQuadOverlay _overlay;
        private int _handleIndex = -1;

        public BaseViewQuadOverlay Overlay => _overlay;

        public int HandleIndex => _handleIndex;

        public bool HasTarget => _overlay != null && _handleIndex >= 0 && _handleIndex < _overlay.PointCount;

        public void SetTarget(BaseViewQuadOverlay overlay, int handleIndex)
        {
            if (overlay == null)
            {
                ClearTarget();
                return;
            }

            if (_overlay != overlay)
            {
                UnsubscribeFromOverlay();
                _overlay = overlay;
                SubscribeToOverlay();
            }

            if (handleIndex < 0 || handleIndex >= _overlay.PointCount)
            {
                _handleIndex = -1;
                Populate();
                return;
            }

            _handleIndex = handleIndex;
            Populate();
        }

        public void ClearTarget()
        {
            UnsubscribeFromOverlay();
            _overlay = null;
            _handleIndex = -1;
            Populate();
        }

        protected override void Initialise()
        {
        }

        protected override void Populate()
        {
            if (_layoutPoint == null)
            {
                return;
            }

            if (!HasTarget)
            {
                SetFieldTexts(string.Empty, string.Empty);
                return;
            }

            Vector2 point = _overlay.GetPoint(_handleIndex);
            SetFieldTexts(Mathf.RoundToInt(point.x).ToString(), Mathf.RoundToInt(point.y).ToString());
        }

        protected override void AddListeners()
        {
            if (_layoutPoint == null)
            {
                return;
            }

            if (_layoutPoint.InputX != null)
            {
                _layoutPoint.InputX.OnValueChanged += OnPointXValueChanged;
                _layoutPoint.InputX.OnValueSubmitted += OnPointXEndEdit;
            }

            if (_layoutPoint.InputY != null)
            {
                _layoutPoint.InputY.OnValueChanged += OnPointYValueChanged;
                _layoutPoint.InputY.OnValueSubmitted += OnPointYEndEdit;
            }
        }

        protected override void RemoveListeners()
        {
            if (_layoutPoint == null)
            {
                return;
            }

            if (_layoutPoint.InputX != null)
            {
                _layoutPoint.InputX.OnValueChanged -= OnPointXValueChanged;
                _layoutPoint.InputX.OnValueSubmitted -= OnPointXEndEdit;
            }

            if (_layoutPoint.InputY != null)
            {
                _layoutPoint.InputY.OnValueChanged -= OnPointYValueChanged;
                _layoutPoint.InputY.OnValueSubmitted -= OnPointYEndEdit;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeFromOverlay();
        }

        private bool OnPointXValueChanged(BoundInputField source, string value)
        {
            UpdateLayoutPoint(value, true);
            return true;
        }

        private bool OnPointXEndEdit(BoundInputField source, string value)
        {
            UpdateLayoutPoint(value, true);
            return true;
        }

        private bool OnPointYValueChanged(BoundInputField source, string value)
        {
            UpdateLayoutPoint(value, false);
            return true;
        }

        private bool OnPointYEndEdit(BoundInputField source, string value)
        {
            UpdateLayoutPoint(value, false);
            return true;
        }

        private void UpdateLayoutPoint(string value, bool isX)
        {
            if (!HasTarget)
            {
                return;
            }

            if (!float.TryParse(value, out float result))
            {
                return;
            }

            Vector2 point = _overlay.GetPoint(_handleIndex);
            if (isX)
            {
                point.x = result;
            }
            else
            {
                point.y = result;
            }

            _overlay.SetPoint(_handleIndex, point);
        }

        private void SubscribeToOverlay()
        {
            if (_overlay == null)
            {
                return;
            }

            View view = _overlay.View;
            if (view != null)
            {
                view.OnChanged.AddListener(OnOverlayViewChanged);
            }
        }

        private void UnsubscribeFromOverlay()
        {
            if (_overlay == null)
            {
                return;
            }

            View view = _overlay.View;
            if (view != null)
            {
                view.OnChanged.RemoveListener(OnOverlayViewChanged);
            }
        }

        private void OnOverlayViewChanged()
        {
            Populate();
        }

        private void SetFieldTexts(string xValue, string yValue)
        {
            if (_layoutPoint == null)
            {
                return;
            }

            if (_layoutPoint.InputX != null)
            {
                _layoutPoint.InputX.Text = xValue;
            }

            if (_layoutPoint.InputY != null)
            {
                _layoutPoint.InputY.Text = yValue;
            }
        }
    }
}
