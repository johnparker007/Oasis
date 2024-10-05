using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.LayoutEditor
{
    public class EditorView : MonoBehaviour, IPointerClickHandler
    {
        public GraphicRaycaster GraphicRaycaster;

        public UnityEvent<List<EditorComponent>> OnPointerClickEvent;

        private void OnEnable()
        {
            Editor.Instance.OnEditorViewEnabled?.Invoke(this);
        }

        private void OnDisable()
        {
            Editor.Instance.OnEditorViewDisabled?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                List<RaycastResult> results = new List<RaycastResult>();
                GraphicRaycaster.Raycast(eventData, results);

                List<EditorComponent> editorComponents = new List<EditorComponent>();
                foreach (RaycastResult result in results)
                {
                    EditorComponent editorComponent = result.gameObject.GetComponent<EditorComponent>();
                    if (editorComponent != null)
                    {
                        editorComponents.Add(editorComponent);
                    }
                }

                if (editorComponents.Count > 0)
                {
                    OnPointerClickEvent?.Invoke(editorComponents);
                }
            }
        }
    }

}

