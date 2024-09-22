using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    public class EditorView : MonoBehaviour
    {
        public GraphicRaycaster GraphicRaycaster;

        public UnityEvent<List<EditorComponent>> OnLeftButtonDown;

        private void Update()
        {
            if(UnityEngine.Input.GetMouseButtonDown(0))
            {
                ProcessLeftButtonDown();
            }
        }

        private void ProcessLeftButtonDown()
        {
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            pointerEventData.position = UnityEngine.Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            GraphicRaycaster.Raycast(pointerEventData, results);

            List<EditorComponent> editorComponents = new List<EditorComponent>();
            foreach (RaycastResult result in results)
            {
                EditorComponent editorComponent = result.gameObject.GetComponent<EditorComponent>();
                if(editorComponent != null)
                {
                    editorComponents.Add(editorComponent);
                }
            }

            if(editorComponents.Count > 0)
            {
                OnLeftButtonDown?.Invoke(editorComponents);
            }
        }
    }

}

