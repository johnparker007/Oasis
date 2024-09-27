using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.UI
{
    public class ColorBox : MonoBehaviour, IPointerClickHandler
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

        public OnChangeEvent onValueChanged { get; set; } = new OnChangeEvent();
        public EndEditEvent onEndEdit { get; set; } = new EndEditEvent();

        private Color _color = Color.white;

        //public void SetColor(Color color, bool supressEvents)
        //{
        //    Color = color;

        //    if(!supressEvents)
        //    {
        //        onValueChanged.Invoke(Color);
        //    }
        //}


        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                Debug.LogError("On color box left click");

                Editor.Instance.ColorPicker.gameObject.SetActive(true);

            }
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
