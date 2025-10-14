using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace Oasis.Utility
{
    public class OasisScrollRectAdditionalEventTargets : MonoBehaviour, IPointerClickHandler
    {
        public List<GameObject> AdditionalEventTargets;

        private OasisScrollRect _oasisScrollRect = null;

        private void Awake()
        {
            _oasisScrollRect = GetComponent<OasisScrollRect>();
        }

        private void Start()
        {
            _oasisScrollRect.OnBeginDragEvent.AddListener(OnOasisScrollRectBeginDrag);
            _oasisScrollRect.OnDragEvent.AddListener(OnOasisScrollRectDrag);
            _oasisScrollRect.OnEndDragEvent.AddListener(OnOasisScrollRectEndDrag);
        }

        private void OnOasisScrollRectBeginDrag(PointerEventData pointerEventData)
        {
            foreach (GameObject additionalEventTarget in AdditionalEventTargets)
            {
                ExecuteEvents.Execute(
                    additionalEventTarget, pointerEventData, ExecuteEvents.beginDragHandler);
            }
        }

        private void OnOasisScrollRectDrag(PointerEventData pointerEventData)
        {
            foreach (GameObject additionalEventTarget in AdditionalEventTargets)
            {
                ExecuteEvents.Execute(
                    additionalEventTarget, pointerEventData, ExecuteEvents.dragHandler);
            }
        }

        private void OnOasisScrollRectEndDrag(PointerEventData pointerEventData)
        {
            foreach (GameObject additionalEventTarget in AdditionalEventTargets)
            {
                ExecuteEvents.Execute(
                    additionalEventTarget, pointerEventData, ExecuteEvents.endDragHandler);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            foreach (GameObject additionalEventTarget in AdditionalEventTargets)
            {
                ExecuteEvents.Execute(
                    additionalEventTarget, eventData, ExecuteEvents.pointerClickHandler);
            }
        }
    }

}
