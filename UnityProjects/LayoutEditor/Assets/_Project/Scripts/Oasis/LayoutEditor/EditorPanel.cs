using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor
{
    [RequireComponent(typeof(Zoom))]
    public class EditorPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        private void Awake()
        {
            Zoom = GetComponent<Zoom>();    
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered = true;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            PointerEntered = false;
        }
    }
}