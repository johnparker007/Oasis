using Oasis.Layout;
using Oasis.UI;
using Oasis.UI.Fields;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelViewQuadInspector : PanelBase
    {
        [SerializeField]
        private FieldString _panelName;

        [SerializeField]
        private FieldVector2 _layoutPointA;
        [SerializeField]
        private FieldVector2 _layoutPointB;
        [SerializeField]
        private FieldVector2 _layoutPointC;
        [SerializeField]
        private FieldVector2 _layoutPointD;

        private struct InputBinding
        {
            public int PointIndex;
            public bool IsX;

            public InputBinding(int pointIndex, bool isX)
            {
                PointIndex = pointIndex;
                IsX = isX;
            }
        }

        private BaseViewQuadOverlay _overlay;
        private int _handleIndex = -1;
        private FieldVector2[] _layoutPoints;
        private readonly Dictionary<BoundInputField, InputBinding> _inputBindings = new Dictionary<BoundInputField, InputBinding>();

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
            if (_layoutPoints != null)
            {
                return;
            }

            _layoutPoints = new[]
            {
                _layoutPointA,
                _layoutPointB,
                _layoutPointC,
                _layoutPointD,
            };
        }

        protected override void Populate()
        {
            Initialise();

            if (_layoutPoints == null)
            {
                return;
            }

            if (!HasTarget)
            {
                ClearAllFieldTexts();
                return;
            }

            int pointCount = Mathf.Min(_overlay.PointCount, _layoutPoints.Length);

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                FieldVector2 field = _layoutPoints[i];
                if (field == null)
                {
                    continue;
                }

                if (i < pointCount)
                {
                    Vector2 point = _overlay.GetPoint(i);
                    SetFieldTexts(field, Mathf.RoundToInt(point.x).ToString(), Mathf.RoundToInt(point.y).ToString());
                }
                else
                {
                    SetFieldTexts(field, string.Empty, string.Empty);
                }
            }
        }

        protected override void AddListeners()
        {
            Initialise();

            if (_layoutPoints == null)
            {
                return;
            }

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                RegisterFieldListeners(_layoutPoints[i], i);
            }
        }

        protected override void RemoveListeners()
        {
            if (_layoutPoints == null)
            {
                return;
            }

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                UnregisterFieldListeners(_layoutPoints[i]);
            }

            _inputBindings.Clear();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeFromOverlay();
        }

        private bool OnPointValueChanged(BoundInputField source, string value)
        {
            return ProcessPointInput(source, value);
        }

        private bool OnPointValueSubmitted(BoundInputField source, string value)
        {
            return ProcessPointInput(source, value);
        }

        private bool ProcessPointInput(BoundInputField source, string value)
        {
            if (!_inputBindings.TryGetValue(source, out InputBinding binding))
            {
                return false;
            }

            UpdateLayoutPoint(binding.PointIndex, binding.IsX, value);
            return true;
        }

        private void UpdateLayoutPoint(int pointIndex, bool isX, string value)
        {
            if (!HasTarget || pointIndex < 0 || pointIndex >= _overlay.PointCount)
            {
                return;
            }

            if (!float.TryParse(value, out float result))
            {
                return;
            }

            Vector2 point = _overlay.GetPoint(pointIndex);
            if (isX)
            {
                point.x = result;
            }
            else
            {
                point.y = result;
            }

            _overlay.SetPoint(pointIndex, point);
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

        private void RegisterFieldListeners(FieldVector2 field, int pointIndex)
        {
            if (field == null)
            {
                return;
            }

            RegisterInputField(field.InputX, pointIndex, true);
            RegisterInputField(field.InputY, pointIndex, false);
        }

        private void RegisterInputField(BoundInputField inputField, int pointIndex, bool isX)
        {
            if (inputField == null || _inputBindings.ContainsKey(inputField))
            {
                return;
            }

            inputField.OnValueChanged += OnPointValueChanged;
            inputField.OnValueSubmitted += OnPointValueSubmitted;
            _inputBindings[inputField] = new InputBinding(pointIndex, isX);
        }

        private void UnregisterFieldListeners(FieldVector2 field)
        {
            if (field == null)
            {
                return;
            }

            UnregisterInputField(field.InputX);
            UnregisterInputField(field.InputY);
        }

        private void UnregisterInputField(BoundInputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.OnValueChanged -= OnPointValueChanged;
            inputField.OnValueSubmitted -= OnPointValueSubmitted;
            _inputBindings.Remove(inputField);
        }

        private void ClearAllFieldTexts()
        {
            if (_layoutPoints == null)
            {
                return;
            }

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                SetFieldTexts(_layoutPoints[i], string.Empty, string.Empty);
            }
        }

        private void SetFieldTexts(FieldVector2 field, string xValue, string yValue)
        {
            if (field == null)
            {
                return;
            }

            if (field.InputX != null)
            {
                field.InputX.Text = xValue;
            }

            if (field.InputY != null)
            {
                field.InputY.Text = yValue;
            }
        }
    }
}
