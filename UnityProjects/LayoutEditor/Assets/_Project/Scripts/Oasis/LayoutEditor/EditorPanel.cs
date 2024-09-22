using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(Zoom))]
    public class EditorPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Zoom Zoom
        {
            get;
            private set;
        }

        public bool PointerEntered
        {
            get;
            private set;
        }

        public bool PointerDown
        {
            get;
            private set;
        }

        private void Awake()
        {
            Zoom = GetComponent<Zoom>();    
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerEntered = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDown = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PointerDown = false;
        }
    }
}