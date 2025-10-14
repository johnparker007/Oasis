using HSVPicker;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.UI
{
    public class ColorBox : MonoBehaviour, IPointerDownHandler
    {
        public Image TargetGraphic;

        public Color Color
        {
            get
            {
                return _color;
            }
            set
            {
                _color = value;
                TargetGraphic.color = _color;
                onValueChanged?.Invoke(_color);
            }
        }

        public ColorPicker ColorPicker
        {
            get
            {
                return Editor.Instance.ColorPicker;
            }
        }

        public OnChangeEvent onValueChanged { get; set; } = new OnChangeEvent();
        public EndEditEvent onEndEdit { get; set; } = new EndEditEvent();

        private Color _color = Color.white;
        private RectTransform _rectTransform = null;
        private bool _pickerShowing = false;

        private void Awake()
        {
            _rectTransform = ColorPicker.gameObject.GetComponent<RectTransform>();
        }

        private void Update()
        {
            if(_pickerShowing && UnityEngine.Input.GetMouseButtonDown(0))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform,
                    UnityEngine.Input.mousePosition,
                    null,
                    out Vector2 localMousePosition);

                if (!_rectTransform.rect.Contains(localMousePosition))
                {
                    HidePicker();
                }
            }
        }

        private void OnDisable()
        {
            HidePicker();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                ShowPicker(eventData);
            }
        }

        private IEnumerator ShowPickerCoroutine(PointerEventData eventData)
        {
            yield return null;

            ColorPicker.gameObject.SetActive(true);
            ColorPicker.CurrentColor = Color;

            ColorPicker.onValueChanged.AddListener(OnPickerValueChanged);

            // we spawn the window offscreen, due to needing to wait a frame in the SetWindowPosition coroutine
            // to get the calculated rectTransform height:
            _rectTransform.position = new Vector3(Screen.width, Screen.height);

            StartCoroutine(SetWindowPositionClampedCoroutine(eventData.position));
        }

        private IEnumerator SetWindowPositionClampedCoroutine(Vector2 clickPosition)
        {
            // wait until end of frame, so rectTransform rect height is calculated
            yield return new WaitForEndOfFrame();

            Vector2 windowPosition = clickPosition;

            windowPosition.x = Mathf.Min(windowPosition.x, Screen.width - _rectTransform.rect.width);
            windowPosition.y = Mathf.Max(windowPosition.y, _rectTransform.rect.height);

            _rectTransform.position = windowPosition;

            _pickerShowing = true;
        }

        private void ShowPicker(PointerEventData eventData)
        {
            StartCoroutine(ShowPickerCoroutine(eventData));
        }

        private void HidePicker()
        {
            ColorPicker.gameObject.SetActive(false);

            ColorPicker.onValueChanged.RemoveListener(OnPickerValueChanged);

            _pickerShowing = false;
        }

        private void OnPickerValueChanged(Color color)
        {
            Color = color;
        }

        public class OnChangeEvent : UnityEvent<Color>
        {
            //public OnChangeEvent();
        }

        public class SubmitEvent : UnityEvent<Color>
        {
            //public SubmitEvent();
        }

        public class EndEditEvent : UnityEvent<Color>
        {
            //public EndEditEvent();
        }

    }



}
