using System.Collections.Generic;
using Oasis.Layout;
using Oasis.UI;
using Oasis.UI.Fields;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelViewQuadInspector : PanelBase
    {
        [SerializeField]
        private FieldVector2 _layoutPointA;

        [SerializeField]
        private FieldVector2 _layoutPointB;

        [SerializeField]
        private FieldVector2 _layoutPointC;

        [SerializeField]
        private FieldVector2 _layoutPointD;

        private BaseViewQuadOverlay _overlay;
        private int _handleIndex = -1;

        private FieldVector2[] _layoutPoints;

        private readonly Dictionary<BoundInputField, InputBinding> _inputBindings = new Dictionary<BoundInputField, InputBinding>();

        public BaseViewQuadOverlay Overlay => _overlay;

        public int HandleIndex => _handleIndex;

        public bool HasTarget => _overlay != null;

        public bool HasSelectedHandle => _overlay != null && _handleIndex >= 0 && _handleIndex < _overlay.PointCount;

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
            _layoutPoints = new[]
            {
                _layoutPointA,
                _layoutPointB,
                _layoutPointC,
                _layoutPointD
            };
        }

        protected override void Populate()
        {
            if (_layoutPoints == null)
            {
                return;
            }

            if (!HasTarget)
            {
                ClearAllFieldTexts();
                return;
            }

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                FieldVector2 field = _layoutPoints[i];
                if (field == null)
                {
                    continue;
                }

                if (i < _overlay.PointCount)
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
            RegisterFieldListeners(_layoutPointA, 0);
            RegisterFieldListeners(_layoutPointB, 1);
            RegisterFieldListeners(_layoutPointC, 2);
            RegisterFieldListeners(_layoutPointD, 3);
        }

        protected override void RemoveListeners()
        {
            if (_inputBindings.Count == 0)
            {
                return;
            }

            List<BoundInputField> keys = new List<BoundInputField>(_inputBindings.Keys);
            foreach (BoundInputField input in keys)
            {
                if (input == null)
                {
                    continue;
                }

                input.OnValueChanged -= OnPointValueChanged;
                input.OnValueSubmitted -= OnPointEndEdit;
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
            UpdateLayoutPoint(source, value);
            return true;
        }

        private bool OnPointEndEdit(BoundInputField source, string value)
        {
            UpdateLayoutPoint(source, value);
            return true;
        }

        private void UpdateLayoutPoint(BoundInputField source, string value)
        {
            if (!HasTarget)
            {
                return;
            }

            if (!float.TryParse(value, out float result))
            {
                return;
            }

            if (!_inputBindings.TryGetValue(source, out InputBinding binding))
            {
                return;
            }

            if (binding.PointIndex < 0 || binding.PointIndex >= _overlay.PointCount)
            {
                return;
            }

            _handleIndex = binding.PointIndex;

            Vector2 point = _overlay.GetPoint(binding.PointIndex);
            if (binding.IsX)
            {
                point.x = result;
            }
            else
            {
                point.y = result;
            }

            _overlay.SetPoint(binding.PointIndex, point);
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

            if (field.InputX != null)
            {
                _inputBindings[field.InputX] = new InputBinding(pointIndex, true);
                field.InputX.OnValueChanged += OnPointValueChanged;
                field.InputX.OnValueSubmitted += OnPointEndEdit;
            }

            if (field.InputY != null)
            {
                _inputBindings[field.InputY] = new InputBinding(pointIndex, false);
                field.InputY.OnValueChanged += OnPointValueChanged;
                field.InputY.OnValueSubmitted += OnPointEndEdit;
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

        private void ClearAllFieldTexts()
        {
            if (_layoutPoints == null)
            {
                return;
            }

            for (int i = 0; i < _layoutPoints.Length; ++i)
            {
                FieldVector2 field = _layoutPoints[i];
                if (field == null)
                {
                    continue;
                }

                SetFieldTexts(field, string.Empty, string.Empty);
            }
        }

        private readonly struct InputBinding
        {
            public readonly int PointIndex;
            public readonly bool IsX;

            public InputBinding(int pointIndex, bool isX)
            {
                PointIndex = pointIndex;
                IsX = isX;
            }
        }
    }
}
