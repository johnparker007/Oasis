using Oasis.LayoutEditor.Panels;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public class InspectorController : MonoBehaviour
    {
        public PanelInspectorLamp PanelInspectorLamp;
        public PanelInspector7Segment PanelInspector7Segment;
        public PanelInspectorReel PanelInspectorReel;
        public PanelInspectorBackground PanelInspectorBackground;
        public PanelInspectorSegmentAlpha PanelInspectorSegmentAlpha;

        private void Awake()
        {
            AddListeners();
            DisableAllPanels();
        }

        public void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            Editor.Instance.SelectionController.OnSelectionChange.AddListener(OnSelectionChange);
        }

        private void RemoveListeners()
        {
            Editor.Instance.SelectionController.OnSelectionChange.RemoveListener(OnSelectionChange);
        }

        private void OnSelectionChange()
        {
            // TODO very simple version, with no MultiEdit for now:

            DisableAllPanels(); // always reinit for now, even if selecting another lamp when one already selected etc
            
            if (Editor.Instance.SelectionController.SelectedEditorComponents.Count == 0)
            {
                return;
            }

            EditorComponent firstSelectedEditorComponent =
                Editor.Instance.SelectionController.SelectedEditorComponents[0];

            if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentLamp))
            {
                PanelInspectorLamp.EditorComponent = firstSelectedEditorComponent;
                PanelInspectorLamp.gameObject.SetActive(true);
            }
            else if (firstSelectedEditorComponent.GetType() == typeof(EditorComponent7Segment))
            {
                PanelInspector7Segment.EditorComponent = firstSelectedEditorComponent;
                PanelInspector7Segment.gameObject.SetActive(true);
            }
            else if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentReel))
            {
                PanelInspectorReel.EditorComponent = firstSelectedEditorComponent;
                PanelInspectorReel.gameObject.SetActive(true);
            }
            else if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentBackground))
            {
                PanelInspectorBackground.EditorComponent = firstSelectedEditorComponent;
                PanelInspectorBackground.gameObject.SetActive(true);
            }
            else if (firstSelectedEditorComponent.GetType() == typeof(EditorComponent16SemicolonSegment))
            {
                //firstSelectedEditorComponent.

                PanelInspectorSegmentAlpha.EditorComponent = firstSelectedEditorComponent;
                PanelInspectorSegmentAlpha.gameObject.SetActive(true);

            }

            //else if(firstSelectedEditorComponent.GetType() == typeof(EditorComponentAlpha))
            //{
            //    PanelInspectorSegmentAlpha.EditorComponent = firstSelectedEditorComponent;
            //    PanelInspectorSegmentAlpha.gameObject.SetActive(true);
            //}
            //else if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentAlpha14))
            //{
            //    // TODO need to work out better way to deal with these variations of a segment alpha
            //    PanelInspectorSegmentAlpha.EditorComponent = firstSelectedEditorComponent;
            //    PanelInspectorSegmentAlpha.gameObject.SetActive(true);
            //}
        }

        private void DisableAllPanels()
        {
            PanelInspectorLamp.gameObject.SetActive(false);
            PanelInspector7Segment.gameObject.SetActive(false);
            PanelInspectorReel.gameObject.SetActive(false);
            PanelInspectorBackground.gameObject.SetActive(false);
            PanelInspectorSegmentAlpha.gameObject.SetActive(false);
        }
    }

}
