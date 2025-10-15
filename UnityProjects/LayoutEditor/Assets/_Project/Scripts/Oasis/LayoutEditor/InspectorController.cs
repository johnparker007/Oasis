using Oasis.LayoutEditor.Panels;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.LayoutEditor
{
    public class InspectorController : MonoBehaviour
    {
        public PanelInspectorLamp PanelInspectorLamp;
        public PanelInspector7Segment PanelInspector7Segment;
        public PanelInspectorReel PanelInspectorReel;
        public PanelInspectorBackground PanelInspectorBackground;
        public PanelInspectorSegmentAlpha PanelInspectorSegmentAlpha;
        public PanelViewQuadInspector PanelViewQuadInspector;

        private readonly HashSet<BaseViewQuadOverlay> _registeredViewQuadOverlays = new HashSet<BaseViewQuadOverlay>();

        public event UnityAction<BaseViewQuadOverlay> OnViewQuadOverlayRegistered;
        public event UnityAction<BaseViewQuadOverlay> OnViewQuadOverlayUnregistered;
        public event UnityAction<BaseViewQuadOverlay, int> OnViewQuadHandleSelectedEvent;
        public event UnityAction<BaseViewQuadOverlay> OnViewQuadHandleSelectionClearedEvent;

        private void Awake()
        {
            Editor.Instance.InspectorController = this;

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

            foreach (BaseViewQuadOverlay overlay in _registeredViewQuadOverlays)
            {
                if (overlay == null)
                {
                    continue;
                }

                overlay.OnHandleSelected -= OnViewQuadHandleSelected;
                overlay.OnHandleSelectionCleared -= OnViewQuadHandleSelectionCleared;

                OnViewQuadOverlayUnregistered?.Invoke(overlay);
            }

            _registeredViewQuadOverlays.Clear();
        }

        private void OnSelectionChange()
        {
            // TODO very simple version, with no MultiEdit for now:

            DisableAllPanels(); // always reinit for now, even if selecting another lamp when one already selected etc

            PanelViewQuadInspector?.ClearTarget();

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
            if (PanelViewQuadInspector != null)
            {
                PanelViewQuadInspector.gameObject.SetActive(false);
            }
        }

        public void RegisterViewQuadOverlay(BaseViewQuadOverlay overlay)
        {
            if (overlay == null || _registeredViewQuadOverlays.Contains(overlay))
            {
                return;
            }

            overlay.OnHandleSelected += OnViewQuadHandleSelected;
            overlay.OnHandleSelectionCleared += OnViewQuadHandleSelectionCleared;

            _registeredViewQuadOverlays.Add(overlay);

            OnViewQuadOverlayRegistered?.Invoke(overlay);
        }

        private void OnViewQuadHandleSelected(BaseViewQuadOverlay overlay, int handleIndex)
        {
            if (PanelViewQuadInspector == null)
            {
                return;
            }

            if (Editor.Instance.SelectionController.SelectedEditorComponents.Count > 0)
            {
                Editor.Instance.SelectionController.DeselectAllObjects();
            }

            Editor.Instance.SelectionController.SuppressNextPointerClickSelection();

            DisableAllPanels();

            PanelViewQuadInspector.SetTarget(overlay, handleIndex);
            PanelViewQuadInspector.gameObject.SetActive(true);

            OnViewQuadHandleSelectedEvent?.Invoke(overlay, handleIndex);
        }

        private void OnViewQuadHandleSelectionCleared(BaseViewQuadOverlay overlay)
        {
            if (PanelViewQuadInspector == null)
            {
                return;
            }

            if (PanelViewQuadInspector.Overlay != overlay)
            {
                return;
            }

            PanelViewQuadInspector.ClearTarget();
            PanelViewQuadInspector.gameObject.SetActive(false);

            OnViewQuadHandleSelectionClearedEvent?.Invoke(overlay);
        }
    }

}
